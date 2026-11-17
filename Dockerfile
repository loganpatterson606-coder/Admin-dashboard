# Build the ASP.NET Core application.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore VailACServer.csproj
RUN dotnet publish VailACServer.csproj -c Release -o /out --no-restore

# Small runtime image. Python is included because Render starts the service
# through render.py; the actual web server remains ASP.NET Core.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
COPY render.py /app/render.py
COPY known-hashes.json /app/known-hashes.json
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV AntiCheat__DatabasePath=/var/data/vailac.db
EXPOSE 10000
ENTRYPOINT ["python3", "/app/render.py"]
