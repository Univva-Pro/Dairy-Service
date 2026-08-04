FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["nuget.config", "./"]
COPY ["nupkg/", "nupkg/"]
COPY ["Dairy.ServiceHub/Dairy.ServiceHub.csproj", "Dairy.ServiceHub/"]
COPY ["Dairy.Context/Dairy.Context.csproj", "Dairy.Context/"]
COPY ["Dairy.DMO/Dairy.DMO.csproj", "Dairy.DMO/"]
COPY ["Dairy.DTO/Dairy.DTO.csproj", "Dairy.DTO/"]
RUN dotnet restore "Dairy.ServiceHub/Dairy.ServiceHub.csproj"

# Copy source code
COPY . .

WORKDIR "/src/Dairy.ServiceHub"
RUN dotnet build "Dairy.ServiceHub.csproj" -c Debug -o /app/build

FROM build AS publish
RUN dotnet publish "Dairy.ServiceHub.csproj" -c Debug -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Dairy.ServiceHub.dll"]
