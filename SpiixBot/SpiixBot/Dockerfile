FROM mcr.microsoft.com/dotnet/sdk:6.0
WORKDIR /app

COPY . /app
RUN dotnet restore "SpiixBot/SpiixBot/SpiixBot.csproj"
RUN dotnet publish "SpiixBot/SpiixBot/SpiixBot.csproj" -c Release -o /app/publish

ENTRYPOINT ["dotnet", "publish/SpiixBot.dll"]