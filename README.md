# SpiixBot
A C# discord bot made by me, primarily for playing music.

# Features
- Music
    - Clear and good quality audio.
    - Advanced queue system.
    - Slightly faster load times (searching) compared to other popular discord music bots.
    - Filters, bassboost and more.
- Sudoku solver

# How to setup
 
## Prerequisites
You need to have docker and docker compose installed. Here's a quick guide on how to install them on debian 10 (you can use whatever as long as docker is supported):

1. [How to install docker](https://www.digitalocean.com/community/tutorials/how-to-install-and-use-docker-on-debian-10) (Only need to follow step 1)
2. [How to install docker compose](https://www.digitalocean.com/community/tutorials/how-to-install-docker-compose-on-debian-10) (Only need to follow step 1)

## Download
```
git clone https://github.com/Terbau/SpiixBot.git
```
Alternatively you could just download the repository as a zip and extract it wherever you want.

## Configuration
All environment variables needs to be configured in `docker-compose.yml`. If you for some reason don't wish to use docker at all, you can put them in `SpiixBot/SpiixBot/appsettings.json`. Running without docker is not recommended but entirely possible. In that case you need to configure and run Lavalink (for now it must be a dev version since this bot requires filters) in its own process.

- (**Required**) You need to have [discord bot account](https://discord.com/developers/applications) configured. Set the value of `DISCORD_BOT_TOKEN` to your bot token.
- (**Required**) You need create a [spotify developer application](https://developer.spotify.com/dashboard/applications). Set the client id and secret using `SPOTIFY_CLIENT_ID` and `SPOTIFY_SECRET`
- (Temporary) (**Required**) For now, you also need a youtube API token. Set it using `YOUTUBE_API_KEY`. This will not longer be required in the future.

## Run
To run the bot, simply cd into the cloned directory and use this command:
```
docker-compose up
```

# Useful Commands
- `!help`

Thats it, thats the useful command.
