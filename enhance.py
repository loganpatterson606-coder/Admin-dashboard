from pathlib import Path
import zipfile
root=Path('/mnt/data/vail_work/VailACServer')
html_path=root/'vercel-dashboard/index.html'
s=html_path.read_text()
extra_css=r'''
/* VailAC motion system — ambient background + restrained micro-interactions */
:root{--motion-ease:cubic-bezier(.2,.75,.25,1)}
html{scroll-behavior:smooth;scroll-padding-top:24px}
body{isolation:isolate;overflow-x:hidden;background-color:#070b11;background-image:radial-gradient(ellipse at 12% 8%,#142b2c 0,transparent 34%),radial-gradient(ellipse at 90% 82%,#17223b 0,transparent 38%),linear-gradient(135deg,#070b11,#0a1019 55%,#080d15)}
body:before,body:after{content:"";position:fixed;inset:-25%;z-index:-1;pointer-events:none;will-change:transform;}
body:before{background:radial-gradient(ellipse at 24% 35%,#a7f45b17 0,transparent 25%),radial-gradient(ellipse at 76% 28%,#59d9e71b 0,transparent 23%),radial-gradient(ellipse at 65% 78%,#7d72ff13 0,transparent 28%);filter:blur(22px);animation:ambientDrift 24s ease-in-out infinite alternate}
body:after{inset:0;background-image:linear-gradient(#b7f36b08 1px,transparent 1px),linear-gradient(90deg,#b7f36b08 1px,transparent 1px);background-size:54px 54px;mask-image:linear-gradient(to bottom,#000 0%,transparent 86%);opacity:.35;animation:gridFloat 32s linear infinite}
@keyframes ambientDrift{0%{transform:translate3d(-3%,1%,0) rotate(-4deg) scale(1)}50%{transform:translate3d(3%,-2%,0) rotate(3deg) scale(1.08)}100%{transform:translate3d(-1%,3%,0) rotate(-2deg) scale(1.03)}}
@keyframes gridFloat{from{background-position:0 0}to{background-position:108px 108px}}
@keyframes riseIn{from{opacity:0;transform:translateY(22px) scale(.985);filter:blur(5px)}to{opacity:1;transform:translateY(0) scale(1);filter:blur(0)}}
@keyframes softGlow{0%,100%{box-shadow:0 0 0 #b8f36b00}50%{box-shadow:0 0 30px #b8f36b12}}
@keyframes pulseDot{0%,100%{box-shadow:0 0 0 0 #b8f36b55,0 0 10px var(--lime)}50%{box-shadow:0 0 0 7px #b8f36b00,0 0 17px var(--lime)}}
@keyframes brandPulse{0%,100%{box-shadow:0 0 18px #b8f36b20;transform:rotate(0) scale(1)}50%{box-shadow:0 0 32px #b8f36b55;transform:rotate(4deg) scale(1.04)}}
@keyframes shimmer{0%{background-position:180% 0}100%{background-position:-180% 0}}
@keyframes scan{0%{transform:translateY(-110%);opacity:0}15%{opacity:.4}85%{opacity:.2}100%{transform:translateY(110vh);opacity:0}}
.shell{position:relative;animation:riseIn .8s var(--motion-ease) both}
.sidebar{position:relative;overflow:hidden;background:linear-gradient(180deg,#0b111bed,#090f17e8);backdrop-filter:blur(18px)}
.sidebar:after{content:"";position:absolute;top:-35%;left:-1px;width:2px;height:40%;background:linear-gradient(transparent,var(--lime),transparent);opacity:.45;animation:scan 11s linear infinite;pointer-events:none}
.brandmark{animation:brandPulse 5s ease-in-out infinite}
.nav a{position:relative;overflow:hidden;transition:color .25s,background .3s,transform .3s var(--motion-ease),padding .3s}
.nav a:before{content:"";position:absolute;left:0;top:18%;bottom:18%;width:2px;border-radius:2px;background:var(--lime);transform:scaleY(0);transition:transform .25s}
.nav a:hover{transform:translateX(4px);background:#192b2c;color:var(--lime)}.nav a.active:before{transform:scaleY(1)}
.panel{position:relative;overflow:hidden;backdrop-filter:blur(14px);box-shadow:0 14px 45px #00000018;transition:transform .35s var(--motion-ease),border-color .35s,box-shadow .35s,background .35s;animation:riseIn .75s var(--motion-ease) both}
.panel:before{content:"";position:absolute;inset:0;pointer-events:none;border-radius:inherit;background:linear-gradient(115deg,transparent 20%,#ffffff05 45%,transparent 70%);background-size:220% 100%;opacity:0;transition:opacity .35s}
.panel:hover{border-color:#40534a;box-shadow:0 20px 55px #00000030,0 0 26px #b8f36b0a;transform:translateY(-3px)}.panel:hover:before{opacity:1;animation:shimmer 2.2s ease}
.stats .panel:nth-child(1){animation-delay:.08s}.stats .panel:nth-child(2){animation-delay:.16s}.stats .panel:nth-child(3){animation-delay:.24s}.contentgrid>.panel:nth-child(1){animation-delay:.30s}.contentgrid>.panel:nth-child(2){animation-delay:.38s}#violations-section{animation-delay:.46s}
.statvalue{transition:color .25s,transform .35s var(--motion-ease)}.stat:hover .statvalue{color:var(--lime);transform:translateY(-2px)}
.btn{position:relative;overflow:hidden;isolation:isolate;transition:transform .22s var(--motion-ease),box-shadow .25s,border-color .25s,background .25s,filter .25s}
.btn:before{content:"";position:absolute;inset:0;z-index:-1;background:linear-gradient(110deg,transparent 20%,#ffffff30 48%,transparent 76%);transform:translateX(-130%);transition:transform .55s var(--motion-ease)}
.btn:hover{transform:translateY(-2px);box-shadow:0 8px 22px #0005;filter:brightness(1.08)}.btn:hover:before{transform:translateX(130%)}.btn:active{transform:translateY(0) scale(.97)}
.field input,.banform input{transition:border-color .25s,box-shadow .25s,background .25s,transform .25s}.field input:hover,.banform input:hover{border-color:#43566a;background:#0d1520}.field input:focus,.banform input:focus{transform:translateY(-1px)}
.toggle{transition:background .25s,border-color .25s,transform .25s}.toggle:hover{background:#111d25;border-color:#40533e;transform:translateX(3px)}.toggle input{transition:background .25s,box-shadow .25s}.toggle input:checked{box-shadow:0 0 13px #b8f36b35}.toggle input:checked:before{transition:left .25s cubic-bezier(.2,.9,.2,1),background .25s}
tbody tr{transition:background .2s,transform .2s}tbody tr:hover{transform:translateX(3px)}
.tag{animation:softGlow 4s ease-in-out infinite}.dot{animation:pulseDot 2.4s ease-in-out infinite}
.loginwrap{position:relative;isolation:isolate;overflow:hidden;background:radial-gradient(ellipse at 50% 45%,#101c20aa,transparent 58%)}
.loginwrap:before{content:"";position:absolute;width:min(75vw,760px);aspect-ratio:1;border:1px solid #b8f36b14;border-radius:50%;box-shadow:0 0 0 50px #b8f36b08,0 0 0 110px #70d9e807,0 0 90px #b8f36b0c;animation:orbit 28s linear infinite;pointer-events:none}
.loginwrap:after{content:"";position:absolute;inset:0;pointer-events:none;background:linear-gradient(transparent,#b8f36b0b 50%,transparent);height:18%;animation:scan 9s linear infinite}
@keyframes orbit{to{transform:rotate(360deg) scale(1.08)}}
.loginbox{z-index:1;position:relative;background:linear-gradient(145deg,#121d28ed,#0d141fee);border-color:#344638;box-shadow:0 28px 100px #0009,0 0 55px #b8f36b0b;animation:riseIn .9s var(--motion-ease) both,softGlow 7s ease-in-out 1s infinite}
.loginbox:after{content:"";position:absolute;inset:0;border-radius:inherit;padding:1px;background:linear-gradient(140deg,#b8f36b55,transparent 35%,#70d9e833 75%,transparent);-webkit-mask:linear-gradient(#000 0 0) content-box,linear-gradient(#000 0 0);-webkit-mask-composite:xor;mask-composite:exclude;pointer-events:none}
.loginbox .brandmark{width:42px;height:42px;font-size:20px}.loginbox h2{letter-spacing:-.055em;font-size:27px}.loginbox .field{animation:riseIn .7s .2s both}.loginbox form button{animation:riseIn .7s .32s both}
.avatar{transition:transform .35s,box-shadow .35s}.avatar:hover{transform:rotate(-8deg) scale(1.1);box-shadow:0 0 22px #b8f36b35}
.empty{transition:color .25s}.sectionhead .tag{white-space:nowrap}
/* Newly inserted rows animate too; JS adds .motion-in after API refresh. */
.motion-in{animation:riseIn .45s var(--motion-ease) both}
@media(max-width:620px){.panel:hover{transform:none}.nav a:hover{transform:none}}
@media(prefers-reduced-motion:reduce){*,*::before,*::after{scroll-behavior:auto!important;animation-duration:.01ms!important;animation-iteration-count:1!important;transition-duration:.01ms!important}body:before,body:after,.loginwrap:before,.loginwrap:after{animation:none!important}}
'''
s=s.replace('</style>',extra_css+'\n</style>')
extra_js=r'''
<script>
// Motion polish for dynamically rendered API content and interactive controls.
(()=>{
 const reduce=window.matchMedia('(prefers-reduced-motion: reduce)').matches;
 if(reduce)return;
 const reveal=(root=document)=>{
  root.querySelectorAll('.panel, .stat, .sectionhead, .toggle, .field, .banform, .tablewrap, .foot').forEach((el,i)=>{
   if(el.dataset.motionReady)return;el.dataset.motionReady='1';
   if(el.closest('.hidden'))return;
   el.classList.add('motion-in');el.style.animationDelay=Math.min(i*35,280)+'ms';
  });
 };
 reveal();
 new MutationObserver(records=>records.forEach(r=>r.addedNodes.forEach(n=>{if(n.nodeType===1)reveal(n)}))).observe(document.body,{childList:true,subtree:true});
 document.addEventListener('click',e=>{const b=e.target.closest('button');if(!b)return;b.animate([{transform:'scale(1)'},{transform:'scale(.96)'},{transform:'scale(1)'}],{duration:230,easing:'ease-out'});});
 const login=document.getElementById('login');const dash=document.getElementById('dash');
 const viewObserver=new MutationObserver(()=>{if(!dash.classList.contains('hidden'))reveal(dash);if(!login.classList.contains('hidden'))reveal(login)});
 viewObserver.observe(login,{attributes:true,attributeFilter:['class']});viewObserver.observe(dash,{attributes:true,attributeFilter:['class']});
})();
</script>
'''
s=s.replace('</body>',extra_js+'\n</body>')
# dashboard deployment page and Render static page use same visual design
for p in [root/'vercel-dashboard/index.html', root/'wwwroot/index.html']:
    p.write_text(s)
Path('/mnt/data/VailAC_Website_Animated.html').write_text(s)
# Keep shared app.js and CSS assets as-is; both dashboards use the same endpoint client.
out=Path('/mnt/data/VailAC_Animated_UI.zip')
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
    for p in root.parent.rglob('*'):
        if p.is_file(): z.write(p,p.relative_to(root.parent))
print(out, out.stat().st_size)
print('/mnt/data/VailAC_Website_Animated.html', Path('/mnt/data/VailAC_Website_Animated.html').stat().st_size)
