# 1. Giai đoạn Build ứng dụng
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file .csproj và khôi phục NuGet packages trước để cache layer hiệu quả
COPY ["WebDocTruyen/WebDocTruyen.csproj", "WebDocTruyen/"]
RUN dotnet restore "WebDocTruyen/WebDocTruyen.csproj"

# Copy toàn bộ mã nguồn và build Release
COPY . .
WORKDIR "/src/WebDocTruyen"
RUN dotnet publish "WebDocTruyen.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Giai đoạn Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render nhận diện PORT, .NET 8 dùng ASPNETCORE_HTTP_PORTS
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "WebDocTruyen.dll"]
