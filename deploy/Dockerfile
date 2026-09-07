FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/L5R.Core/ src/L5R.Core/
COPY src/L5R.Server/ src/L5R.Server/
RUN dotnet publish src/L5R.Server/L5R.Server.csproj -c Release -o /published

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN mkdir /data && chown "$APP_UID" /data
COPY --from=build /published .
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
ENV L5R__StoragePath=/data
EXPOSE 8080
ENTRYPOINT ["dotnet", "L5R.Server.dll"]
