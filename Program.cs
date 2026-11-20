using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using VailACServer;
var b=WebApplication.CreateBuilder(args);var cfg=b.Configuration.GetSection("AntiCheat");
string Secret(string k){var v=cfg[k]??throw new Exception($"Missing AntiCheat:{k}");if(v.StartsWith("CHANGE_"))throw new Exception($"Set AntiCheat:{k} via environment variable.");return v;}
var adminPassword=Secret("AdminPassword");var serverKey=Secret("GameServerKey");
var manifest=Path.Combine(b.Environment.ContentRootPath,cfg["KnownHashesFile"]??"known-hashes.json");
var hashes=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(manifest))??new();
foreach(var (p,h) in hashes)if(p.Contains("..")||p.StartsWith('/')||h.Length!=64||!h.All(Uri.IsHexDigit)||h.StartsWith("REPLACE_"))throw new Exception($"Replace invalid hash for {p} in known-hashes.json");
var versions=cfg.GetSection("AllowedVersions").Get<string[]>()??Array.Empty<string>();var db=new Database(cfg["DatabasePath"]??"vailac.db");var state=new RuntimeState();
var lifetime=cfg.GetValue("SessionLifetimeMinutes",60);var challengeAge=cfg.GetValue("ChallengeMaxAgeSeconds",90);var maxSpeed=cfg.GetValue("DefaultMaxSpeed",12d);var maxVertical=cfg.GetValue("DefaultMaxVerticalSpeed",14d);var maxTeleport=cfg.GetValue("DefaultMaxTeleportDistance",8d);var tolerance=cfg.GetValue("MovementTolerance",1.5d);
b.Services.AddSingleton(db);b.Services.AddSingleton(state);
b.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{o.Cookie.Name="VailAC.Admin";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=cfg.GetValue("CookieSecure",true)?CookieSecurePolicy.Always:CookieSecurePolicy.SameAsRequest;o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=true;o.Events.OnRedirectToLogin=x=>{x.Response.StatusCode=401;return Task.CompletedTask;};});
b.Services.AddAuthorization();b.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddFixedWindowLimiter("public",x=>{x.PermitLimit=50;x.Window=TimeSpan.FromMinutes(1);x.QueueLimit=0;});o.AddFixedWindowLimiter("login",x=>{x.PermitLimit=5;x.Window=TimeSpan.FromMinutes(5);x.QueueLimit=0;});});
var app=b.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Serve the dashboard at /. Static files normally serve wwwroot/index.html;
// this explicit route also checks for index.html in the publish root.
app.MapGet("/", (IWebHostEnvironment env) =>
{
    var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
    var candidates = new[]
    {
        Path.Combine(webRoot, "index.html"),
        Path.Combine(env.ContentRootPath, "index.html")
    };
    var indexFile = candidates.FirstOrDefault(File.Exists);
    return indexFile is not null
        ? Results.File(indexFile, "text/html; charset=utf-8")
        : Results.Problem(
            title: "VailAC dashboard files were not deployed",
            detail: "The server is running, but index.html was not found. Make sure VailACServer/wwwroot/index.html and the dashboard assets are included in the Docker build.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
});
app.MapGet("/health",()=>Results.Ok(new{ok=true}));
app.MapPost("/ac/handshake",(HandshakeRequest r)=>{
 if(string.IsNullOrWhiteSpace(r.PlayerId)||r.PlayerId.Length>128||string.IsNullOrWhiteSpace(r.ClientNonce)||Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-r.Timestamp)>60000)return Results.BadRequest(new HandshakeResponse(false,Error:"Invalid identity, nonce or timestamp."));
 if(!versions.Contains(r.GameVersion))return Results.BadRequest(new HandshakeResponse(false,Error:"Game version not allowed."));
 if(db.IsBanned(r.PlayerId))return Results.Json(new HandshakeResponse(false,Error:"Player banned."),statusCode:403);
 var token=Hex(32);var nonce=Hex(32);var challenge=Hex(32);var key=Derive(token,nonce,r.ClientNonce);
 state.Sessions[r.PlayerId]=new AcSession{PlayerId=r.PlayerId,Token=token,ServerNonce=nonce,ClientNonce=r.ClientNonce,HmacKey=key,Challenge=challenge,Issued=DateTimeOffset.UtcNow,ChallengeIssued=DateTimeOffset.UtcNow};
 return Results.Ok(new HandshakeResponse(true,token,nonce,challenge));
}).RequireRateLimiting("public");
app.MapPost("/ac/integrity",(IntegrityReport r)=>{
 if(r.FileHashes is null||r.RunningProcesses is null||r.FileHashes.Count>2000||r.RunningProcesses.Count>500)return Results.BadRequest(new IntegrityResponse(false,"Malformed report.",1013));
 if(!state.Sessions.TryGetValue(r.PlayerId,out var s))return Results.Unauthorized();
 lock(s.Gate){
  if(s.Revoked||s.Token!=r.SessionToken||DateTimeOffset.UtcNow-s.Issued>TimeSpan.FromMinutes(lifetime))return Results.Unauthorized();
  if(Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-r.Timestamp)>challengeAge*1000L)return Reject(r,s,1009,"Timestamp skew.",1);
  if(DateTimeOffset.UtcNow-s.ChallengeIssued>TimeSpan.FromSeconds(challengeAge))return Reject(r,s,1008,"Challenge expired.",2);
  if(db.IsBanned(r.PlayerId,r.MachineId))return Results.Json(new IntegrityResponse(false,"Player or device banned.",1010),statusCode:403);
  var canonical=$"{r.PlayerId}|{r.Timestamp}|{r.SessionToken}|{r.DetectedDebugger}|{r.DetectedVirtualMachine}|{r.FileHashes.Count}|{r.RunningProcesses.Count}";
  if(!Equal(Hmac(canonical,s.HmacKey),r.ReportHmac??""))return Reject(r,s,1007,"Report HMAC invalid.",4);
  if(!Equal(Hmac(s.Challenge,s.HmacKey),r.ChallengeResponse??""))return Reject(r,s,1008,"Challenge invalid or replayed.",5);
  var seen=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  foreach(var f in r.FileHashes){if(string.IsNullOrWhiteSpace(f.RelativePath)||f.RelativePath.Contains("..")||f.Sha256?.Length!=64||!f.Sha256.All(Uri.IsHexDigit))return Reject(r,s,1013,"Malformed file hash.",3);if(!seen.TryAdd(f.RelativePath.Replace('\\','/').TrimStart('/'),f.Sha256.ToLowerInvariant()))return Reject(r,s,1013,"Duplicate file path.",3);}
  foreach(var (path,expected) in hashes){if(!seen.TryGetValue(path.Replace('\\','/').TrimStart('/'),out var actual))return Reject(r,s,1013,$"Missing critical file: {path}",5);if(!Equal(expected.ToLowerInvariant(),actual))return Reject(r,s,1001,$"Hash mismatch: {path}",8);}
  if(!r.ModuleBaseIntact||!r.ImportTableIntact)return Reject(r,s,1012,"Client module/import check failed.",5);
  if(r.DetectedDebugger)return Reject(r,s,1003,"Client reports debugger attached.",3);
  if(r.DetectedVirtualMachine)db.Violation(r.PlayerId,r.MachineId,1004,"VM indicator (telemetry only).",1);
  var suspicious=r.RunningProcesses.Where(Suspicious).Distinct().Take(20).ToArray();if(suspicious.Length>0)db.Violation(r.PlayerId,r.MachineId,1002,"Client-reported process indicators: "+string.Join(", ",suspicious),2);
  s.MachineId=r.MachineId;s.Challenge=Hex(32);s.ChallengeIssued=DateTimeOffset.UtcNow;return Results.Ok(new IntegrityResponse(true,"OK",0,s.Challenge));
 }
}).RequireRateLimiting("public");
// These two routes are for a trusted game server only, never a player client.
app.MapGet("/server/players/{id}/policy",(HttpContext c,string id)=>{if(!ServerAuth(c,serverKey))return Results.Unauthorized();if(db.IsBanned(id))return Results.Json(new{banned=true},statusCode:403);return Results.Ok(db.Policy(id,maxSpeed,maxVertical,maxTeleport));});
app.MapPost("/server/movement/validate",(HttpContext c,MovementSample m)=>{
 if(!ServerAuth(c,serverKey))return Results.Unauthorized();if(db.IsBanned(m.PlayerId))return Results.Json(new{accepted=false,reason="banned"},statusCode:403);
 if(!new[]{m.X,m.Y,m.Z,m.VelocityX,m.VelocityY,m.VelocityZ,m.DeltaSeconds}.All(double.IsFinite)||m.DeltaSeconds<=0||m.DeltaSeconds>2||Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-m.Timestamp)>10000)return Results.BadRequest();
 var p=db.Policy(m.PlayerId,maxSpeed,maxVertical,maxTeleport);var horizontal=Math.Sqrt(m.VelocityX*m.VelocityX+m.VelocityZ*m.VelocityZ);
 if(horizontal>p.MaxSpeed+tolerance)return MoveFail(m,2001,"Horizontal speed exceeded.",3);
 if(Math.Abs(m.VelocityY)>p.MaxVerticalSpeed+tolerance)return MoveFail(m,2003,p.CanFly?"Granted flight exceeds configured vertical-speed cap.":"Vertical speed exceeded; flight not permitted.",3);
 if(state.Positions.TryGetValue(m.PlayerId,out var prev)){var dx=m.X-prev.X;var dy=m.Y-prev.Y;var dz=m.Z-prev.Z;var dist=Math.Sqrt(dx*dx+dy*dy+dz*dz);var allowed=p.MaxSpeed*m.DeltaSeconds+tolerance;if(dist>allowed&&!(m.IsTeleport&&p.CanTeleport&&dist<=p.MaxTeleportDistance))return MoveFail(m,2002,"Displacement exceeded movement envelope.",4);}
 state.Positions[m.PlayerId]=(m.X,m.Y,m.Z);return Results.Ok(new{accepted=true,canFly=p.CanFly,canTeleport=p.CanTeleport});
});
app.MapPost("/admin/login",async(HttpContext c,AdminLogin x)=>{if(!Equal(x.Password,adminPassword))return Results.Unauthorized();var id=new ClaimsIdentity(new[]{new Claim(ClaimTypes.Name,"VailAC Admin"),new Claim(ClaimTypes.Role,"Admin")},CookieAuthenticationDefaults.AuthenticationScheme);await c.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(id));return Results.Ok(new{authenticated=true});}).AllowAnonymous().RequireRateLimiting("login");
app.MapPost("/admin/logout",async(HttpContext c)=>{await c.SignOutAsync();return Results.Ok();}).RequireAuthorization();
app.MapGet("/admin/me",()=>Results.Ok(new{authenticated=true})).RequireAuthorization();
app.MapGet("/admin/violations",()=>Results.Ok(db.Violations())).RequireAuthorization();app.MapGet("/admin/bans",()=>Results.Ok(db.Bans())).RequireAuthorization();
app.MapGet("/admin/players/{id}/policy",(string id)=>Results.Ok(db.Policy(id,maxSpeed,maxVertical,maxTeleport))).RequireAuthorization();
app.MapPost("/admin/players/{id}/grant",(HttpContext c,string id,GrantRequest g)=>{if(!DashboardRequest(c))return Results.StatusCode(403);if(string.IsNullOrWhiteSpace(id)||id.Length>128||g.MaxSpeed is <=0 or >100||g.MaxVerticalSpeed is <=0 or >100)return Results.BadRequest();db.Grant(id,g,maxSpeed,maxVertical);return Results.Ok(db.Policy(id,maxSpeed,maxVertical,maxTeleport));}).RequireAuthorization();
app.MapPost("/admin/ban",(HttpContext c,BanRequest r)=>{if(!DashboardRequest(c))return Results.StatusCode(403);if(string.IsNullOrWhiteSpace(r.PlayerId))return Results.BadRequest();db.Ban(r.PlayerId,r.MachineId,r.Reason);if(state.Sessions.TryGetValue(r.PlayerId,out var s))s.Revoked=true;return Results.Ok(new{banned=true});}).RequireAuthorization();
app.MapDelete("/admin/ban/{id}",(HttpContext c,string id)=>{if(!DashboardRequest(c))return Results.StatusCode(403);db.Unban(id);return Results.Ok(new{unbanned=true});}).RequireAuthorization();
app.Run();
IResult Reject(IntegrityReport r,AcSession s,int code,string reason,int score){db.Violation(r.PlayerId,r.MachineId,code,reason,score);s.Revoked=true;return Results.Ok(new IntegrityResponse(false,reason,code));}
IResult MoveFail(MovementSample m,int code,string reason,int score){db.Violation(m.PlayerId,null,code,reason,score);return Results.Ok(new{accepted=false,reason,violationCode=code});}
static bool DashboardRequest(HttpContext c)=>c.Request.Headers.TryGetValue("X-VailAC-Dashboard",out var v)&&v=="1";
static bool ServerAuth(HttpContext c,string k)=>c.Request.Headers.TryGetValue("X-Game-Server-Key",out var v)&&Equal(v.ToString(),k);
static bool Suspicious(string n){var x=n.ToLowerInvariant();return new[]{"cheatengine","x64dbg","x32dbg","ollydbg","windbg","ida","dnspy","processhacker","xenos","injector","trainer","cheat"}.Any(x.Contains);}
static string Hex(int n)=>Convert.ToHexString(RandomNumberGenerator.GetBytes(n)).ToLowerInvariant();
static string Derive(string t,string s,string c)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{t}:{s}:{c}"))).ToLowerInvariant();
static string Hmac(string msg,string key){using var h=new HMACSHA256(Convert.FromHexString(key));return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(msg))).ToLowerInvariant();}
static bool Equal(string a,string b){var x=Encoding.UTF8.GetBytes(a);var y=Encoding.UTF8.GetBytes(b);return x.Length==y.Length&&CryptographicOperations.FixedTimeEquals(x,y);}
sealed class RuntimeState{public ConcurrentDictionary<string,AcSession> Sessions{get;}=new();public ConcurrentDictionary<string,(double X,double Y,double Z)> Positions{get;}=new();}
sealed class AcSession{public object Gate{get;}=new();public required string PlayerId{get;init;}public required string Token{get;init;}public required string ServerNonce{get;init;}public required string ClientNonce{get;init;}public required string HmacKey{get;init;}public required string Challenge{get;set;}public DateTimeOffset Issued{get;init;}public DateTimeOffset ChallengeIssued{get;set;}public string? MachineId{get;set;}public bool Revoked{get;set;}}
