# The production image: the API with the React build in wwwroot, one container.
# Built on the server by deploy/deploy.sh (see docs/DEPLOY.md); also runs locally for a full check.

# 1. React build
FROM node:24-alpine AS web
WORKDIR /web
COPY src/web/package.json src/web/package-lock.json ./
RUN npm ci
COPY src/web/ ./
RUN npm run build

# 2. API build; the project files first, so packages are restored again only when they change
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY src/Zalihe.Domain/Zalihe.Domain.csproj src/Zalihe.Domain/
COPY src/Zalihe.Application/Zalihe.Application.csproj src/Zalihe.Application/
COPY src/Zalihe.Infrastructure/Zalihe.Infrastructure.csproj src/Zalihe.Infrastructure/
COPY src/Zalihe.Web/Zalihe.Web.csproj src/Zalihe.Web/
RUN dotnet restore src/Zalihe.Web/Zalihe.Web.csproj
COPY src/Zalihe.Domain/ src/Zalihe.Domain/
COPY src/Zalihe.Application/ src/Zalihe.Application/
COPY src/Zalihe.Infrastructure/ src/Zalihe.Infrastructure/
COPY src/Zalihe.Web/ src/Zalihe.Web/
RUN dotnet publish src/Zalihe.Web/Zalihe.Web.csproj -c Release -o /app --no-restore

# 3. Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
# Data Protection keys live in a volume mounted here; the folder is created owned by the app user,
# so a new volume takes that owner and the app (not root) can write to it.
RUN mkdir -p /data/keys && chown "$APP_UID" /data/keys
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Zalihe.Web.dll"]
