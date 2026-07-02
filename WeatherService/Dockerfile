# Use the ASP.NET Core runtime image (this runs your published app)
# This image is lightweight and ideal for production.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

# Set the working directory inside the container
WORKDIR /app

# Expose port 5000 because your gRPC server will listen on this port
# ACA ingress will map 443 -> 5000 internally.
EXPOSE 5000
EXPOSE 7010

# Clear Docker Desktop auto-binding
ENV ASPNETCORE_URLS=
ENV DOTNET_URLS=

# Set the environment to Production for your gRPC server
ENV ASPNETCORE_ENVIRONMENT=Production

# Build stage: use the full .NET SDK to compile and publish your project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Set working directory for the build stage
WORKDIR /src

# Copy only the project file first (best practice for Docker layer caching)
COPY ["WeatherService.csproj", "./"]

# Restore dependencies
RUN dotnet restore "./WeatherService.csproj"

# Copy all project files into the container
COPY . .

# Publish the project in Release mode into /app/publish
# This produces the optimized output used in the final image.
RUN dotnet publish "WeatherService.csproj" -c Release -o /app/publish


# Final stage: runtime image with your published app
FROM base AS final

# Set working directory again for the final stage
WORKDIR /app

# Copy the published output from the build stage into the final image
COPY --from=build /app/publish .

# Start your gRPC server
# Replace WeatherService.dll with your actual DLL name if different.
ENTRYPOINT ["dotnet", "WeatherService.dll"]
