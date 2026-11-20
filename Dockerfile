# Build the VailAC ASP.NET Core application.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Docker build context should be the VailACServer project root.
COPY . .

# The VailAC dashboard HTML lives in the project root. Put it at the
# ASP.NET Core web root as index.html so https://your-service/ serves it.
RUN set -eu; \
    mkdir -p /src/wwwroot; \
    if [ -f /src/VailAC_Website_Animated.html ]; then \
      cp /src/VailAC_Website_Animated.html /src/wwwroot/index.html; \
    elif [ -f /src/VailAC_Website_Animated.html.txt ]; then \
      cp /src/VailAC_Website_Animated.html.txt /src/wwwroot/index.html; \
    elif [ -f /src/VailAC.html ]; then \
      cp /src/VailAC.html /src/wwwroot/index.html; \
    elif [ -f /src/wwwroot/index.html ]; then \
      echo 'Using existing wwwroot/index.html'; \
    else \
      echo 'ERROR: Put VailAC_Website_Animated.html (or VailAC.html) in the VailACServer project root.'; \
      exit 1; \
    fi; \
    if [ -f /src/app.js ]; then \
      cp /src/app.js /src/wwwroot/app.js; \
    elif [ -f /src/vercel-dashboard/app.js ] && [ ! -f /src/wwwroot/app.js ]; then \
      cp /src/vercel-dashboard/app.js /src/wwwroot/app.js; \
    fi

RUN dotnet restore VailACServer.csproj
RUN dotnet publish VailACServer.csproj -c Release -o /out --no-restore

# Runtime image for Render.
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
