using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.DiscordObjects;


namespace Oxide.Plugins
{
	[Info("Discord CallAdmin", "evlad", "0.1.2")]
	[Description("Creates a live chat between a specific player and Admins through Discord")]

	internal class DiscordCallAdmin : CovalencePlugin
	{
		
		#region Variables

		[PluginReference]
		private Plugin DiscordCore;

		private DiscordClient _discordClient;
		private Guild _discordGuild;

		#endregion

		#region Config

		PluginConfig _config;

		private class PluginConfig
		{
			public string CategoryID;
			public string ReplyCommand;
			public string SteamProfileIcon;

			public static PluginConfig Default()
			{
				return new PluginConfig
				{
					CategoryID = "",
					ReplyCommand = "r",
					SteamProfileIcon = ""
				};
			}
		}


		protected override void LoadConfig()
		{
			base.LoadConfig();
			_config = Config.ReadObject<PluginConfig>();
		}
		protected override void LoadDefaultConfig() => _config = PluginConfig.Default();
		protected override void SaveConfig() => Config.WriteObject(_config);


		#endregion

		#region Initialization & Setup

		// private void OnServerInitialized()
		// {
		// 	DiscordCore?.Call("RegisterPluginForExtensionHooks", this);
		// }

		private void Init()
		{
			_config = Config.ReadObject<PluginConfig>();

			if (_config.ReplyCommand.Length > 0) {
				AddCovalenceCommand(_config.ReplyCommand, "ReplyCommand");
			}
		}

		private void Loaded()
		{
			if (DiscordCore == null)
			{
				PrintError("Missing plugin dependency DiscordCore: https://umod.org/plugins/discord-core");
				return;
			}
			if (!IsDiscordReady())
				return;
			Setup();
		}

		private void OnDiscordCoreReady()
		{
			Setup();
		}

		private void Setup()
		{
			DiscordCore?.Call("RegisterPluginForExtensionHooks", this);
			_discordClient = (DiscordClient)DiscordCore?.Call("GetClient");
			_discordGuild = _discordClient.DiscordServer;

			List<Channel> channels = (List<Channel>)DiscordCore?.Call("GetAllChannels");
			bool categoryExists = false;

			foreach (var channel in channels) {
				if (channel.id == _config.CategoryID && channel.type == ChannelType.GUILD_CATEGORY)
					categoryExists = true;
				if (channel.parent_id == _config.CategoryID)
					SubscribeToChannel(channel);
			}
			if (!categoryExists)
				throw new Exception("Category with ID: \"" + _config.CategoryID + "\" doesn't exist!");
		}

		#endregion

		#region Helpers

		[HookMethod("StartLiveChat")]
		public bool StartLiveChat(string playerID)
		{
			BasePlayer player = GetPlayerByID(playerID);
			if (!player) {
				PrintError("Player with ID \"" + playerID + "\" wasn't found!");
				return false;
			}

			if (_discordGuild.channels.Find(c => c.name == playerID) != null) {
				PrintError("Player \"" + playerID + "\" already has an opened chat!");
				return false;
			}

			Channel channel = new Channel{
				name = playerID,
				type = ChannelType.GUILD_TEXT,
				permission_overwrites = new List<Overwrite>{
					new Overwrite{
						id = _discordClient.DiscordServer.id, // @everyone
						type = "role",
						deny = 0x00000400 // View messages
					}
				}
			};

			_discordGuild.CreateGuildChannel(_discordClient, channel, createdChannel => {
				createdChannel.parent_id = _config.CategoryID;
				createdChannel.ModifyChannel(_discordClient, createdChannel, _ =>
				{
					createdChannel.CreateMessage(_discordClient, $"@here New chat opened!\nYou are now talking to `{player.displayName}`");
					SubscribeToChannel(createdChannel);
				});
			});

			return true;
		}


		[HookMethod("StopLiveChat")]
		public void StopLiveChat(string playerID, string reason = null)
		{
			Channel channel = (Channel)DiscordCore.Call("GetChannel", playerID);
			if (channel == null)
				return;

			DiscordCore.Call("SendMessageToChannel", channel.id, $"@here The chat has been closed. Self-deletion in 10 seconds..." + (reason != null ? "\nReason: " + reason : ""));
			timer.Once(10f, () =>
			{
				channel.DeleteChannel(_discordClient);
			});
		}

		private void SubscribeToChannel(Channel channel)
		{
			DiscordCore?.Call("SubscribeChannel", channel.id, this, new Func<Message, object>((message) => {
				JObject userMessage = (JObject)DiscordCore?.Call("GetUserDiscordInfo", message.author.id);
				if (userMessage == null) {
					message.CreateReaction(_discordClient, "❌");
					return null;
				}

				if (message.content == "!close") {
					channel.DeleteChannel(_discordClient);
					return null;
				}
				if (!SendMessageToPlayerID(channel.name, "<color=#9c0000>Admin Live Chat</color>\n" + message.content + "\n\n\n<color=#dadada>Reply by typing </color><color=#bd8f8f>/" + _config.ReplyCommand +" <message></color>")) {
					DiscordCore.Call("SendMessageToChannel", channel.id, "User is not connected, the live chat will close in 5 seconds...");
					timer.Once(5f, () =>
					{
						channel.DeleteChannel(_discordClient);
					});
				}

				message.CreateReaction(_discordClient, "✅");

				return null;
			}));
		}

		private BasePlayer GetPlayerByID(string ID)
		{
			return BasePlayer.FindByID(Convert.ToUInt64(ID));
		}

		private bool SendMessageToPlayerID(string playerID, string message)
		{
			BasePlayer player = GetPlayerByID(playerID);
			if (player == null)
				return false;
			
			player.Command("chat.add", 0, _config.SteamProfileIcon, message);
			return true;
		}

		private bool IsDiscordReady()
		{
			return DiscordCore?.Call<bool>("IsReady") ?? false;
		}

		#endregion

		#region Events

		private void Discord_ChannelDelete(Channel channel)
        {
			if (channel.parent_id == _config.CategoryID)
				SendMessageToPlayerID(channel.name, "<color=#33b5e5>An admin closed the live chat.</color>");
        }

		private void OnUserDisconnected(IPlayer player)
		{
			StopLiveChat(player.Id, "Player disconnected.");
		}

		#endregion

		#region Commands

		[Command("calladmin")]
		private void CallAdminCommand(IPlayer player, string command, string[] args)
		{
			if (!IsDiscordReady())
				return;
			player.Command("chat.add", 0, _config.SteamProfileIcon,
				StartLiveChat(player.Id) ?
					"<color=#00C851>Admins have been notified, they'll get in touch with you as fast as possible.</color>" :
					"<color=#ff4444>You've already notified the admins, please wait until an admin responds.</color>"
			);
		}

		private void ReplyCommand(IPlayer player, string command, string[] args)
		{
			if (!IsDiscordReady())
				return;
			if (args.Length < 1) {
				player.Reply("Usage: /r <message>");
				return;
			}
			string pid = player.Id;
			Channel replyChannel = (Channel)DiscordCore?.Call("GetChannel", pid);

			if (replyChannel == null || replyChannel.name != player.Id) {
				player.Command("chat.add", 0, _config.SteamProfileIcon, "You have no live chat in progress.");
				return;
			}

			replyChannel.GetChannelMessages(_discordClient, messages =>
			{
				if (messages.Count < 2) {
					player.Command("chat.add", 0, _config.SteamProfileIcon, "<color=#ff4444>Wait until an admin responds.</color>");
					return;
				}

				DateTime now = DateTime.Now;
				DiscordCore?.Call("SendMessageToChannel", replyChannel.id, $"({now.Hour.ToString() + ":" + now.Minute.ToString()}) {player.Name}: {string.Join(" ", args)}");
				player.Command("chat.add", 0, _config.SteamProfileIcon, "Your message has been sent to the admins!");
			});
		}

		#endregion
	}
}