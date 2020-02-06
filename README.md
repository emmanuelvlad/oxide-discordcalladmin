Players can use a command that creates a channel and notifies admins in discord, admins can reply from within this channel to the player.
## Permissions

- `discordcalladmin.use` - Allows the player to use /calladmin

## Commands

- `/calladmin`  
Creates a channel under `CategoryID` and notifies admins.
- `/r [message]`  
Replies to the admin. Default: `r`, configurable in `ReplyCommand`

## Configuration

```json
{
  "CategoryID": "",
  "ReplyCommand": "r",
  "SteamProfileIcon": "",
  "AllowedRoles": [
	  "675027571959660556"
  ]
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