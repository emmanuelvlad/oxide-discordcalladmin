# Discord Call Admin for Oxide Rust

Version: 0.1.2

## Dependencies

**[Discord](https://umod.org/extensions/discord)**  
**[DiscordCore](https://umod.org/plugins/discord-core)** ^0.14.4

## Configuration

```json
{
  "CategoryID": "",
  "ReplyCommand": "r",
  "SteamProfileIcon": ""
}
```

## API

### StartLiveChat
```cs
bool StartLiveChat(string playerID)
```

### StopLiveChat
```cs
void StopLiveChat(string playerID, string reason = null)
```