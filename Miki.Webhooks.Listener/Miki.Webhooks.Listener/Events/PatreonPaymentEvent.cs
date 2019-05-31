﻿using Miki.Discord;
using Miki.Discord.Common;
using Miki.Logging;
using Miki.Models;
using Newtonsoft.Json;
using Sentry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;

namespace Miki.Webhooks.Listener.Events
{
	public class PatreonPaymentEvent : IWebhookEvent
	{
        Task _backgroundTask;

		public class PatreonUserReward
		{
			[JsonProperty("user_id")]
			public ulong UserId { get; set; }

			[JsonProperty("keys_rewarded")]
			public int KeysRewarded { get; set; }
		}

		public string[] AcceptedUrls => new []{ "patreon" };

		public async Task OnMessage(string response)
		{
			List<PatreonUserReward> rewards = JsonConvert.DeserializeObject<List<PatreonUserReward>>(response);
            if(_backgroundTask.Status != TaskStatus.Running)
            {
                SentrySdk.CaptureException(new OperationCanceledException("Patreon payment event overridden."));
            }
            _backgroundTask = ProcessPayments(rewards); 
		}

        private async Task ProcessPayments(List<PatreonUserReward> paymentEvents)
        {
            var context = new WebhookContext();
            foreach (var reward in paymentEvents)
            {
                while (reward.KeysRewarded > 0)
                {
                    List<string> keys = new List<string>();

                    for (int i = 0; i < Math.Min(10, reward.KeysRewarded); i++)
                    {
                        var key = (await context.DonatorKey.AddAsync(new DonatorKey
                        {
                            StatusTime = TimeSpan.FromDays(31)
                        })).Entity;

                        await context.SaveChangesAsync();

                        keys.Add(key.Key.ToString());
                    }

                    reward.KeysRewarded -= keys.Count;

                    try
                    {
                        var channel = await Program.Discord.CreateDMChannelAsync(reward.UserId);
                        await Program.Discord.SendMessageAsync(channel.Id, new MessageArgs
                        {
                            embed = new EmbedBuilder()
                            {
                                Title = "🎉 You donated through Patreon!",
                                Description = "From the bottom of my heart, I want to thank you for supporting my hobby and my passion project!\n\nWith love, Veld#0001"
                            }.SetColor(221, 46, 68)
                            .AddField("Here are your key(s)!", "\n```\n" + string.Join("\n", keys) + "```")
                            .AddField("How to redeem this key?", $"use this command `>redeemkey`")
                            .ToEmbed()
                        });
                    }
                    catch(Exception e)
                    {
                        SentrySdk.CaptureException(e);
                        Log.Error(e);
                    }

                    await Task.Delay(1000);
                }
            }
            context.Dispose();
        }
	}
}
