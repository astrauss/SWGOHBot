using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System.Net.Http;
using System.Net.Http.Headers;
using SWGOHBot.Model;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace SWGOHBot.Bots
{
    public class TheBot : ActivityHandler
    {
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            Mention[] m = turnContext.Activity.GetMentions();
            var messageText = turnContext.Activity.Text;
            Log.Verbose(string.Format("Orgininal Message: {0}", messageText));
            for (int i = 0; i < m.Length; i++)
            {
                Log.Verbose(string.Format("Mention {0}: ID = {1}, Name = {2}, Role = {3}", i, m[i].Mentioned.Id, m[i].Mentioned.Name, m[i].Mentioned.Role));
                if (m[i].Mentioned.Id == turnContext.Activity.Recipient.Id)
                {
                    //Bot is in the @mention list.
                    //The below example will strip the bot name out of the message, so you can parse it as if it wasn't included. Note that the Text object will contain the full bot name, if applicable.
                    if (m[i].Text != null)
                    {
                        messageText = messageText.Replace(m[i].Text, "");
                        Log.Verbose(string.Format("Stripped Message: {0}", messageText));
                    }
                }
            }
            messageText = messageText.ToLowerInvariant().Trim();
            
            Log.Verbose(string.Format("Receiving Activitiy, Text: {0}", messageText));
            string reply = String.Empty;

            if (messageText == "help")
            {
                Log.Verbose(string.Format("Help = true"));
                await SendSuggestedActionsAsync(turnContext, cancellationToken);
            }
            else
            {
                Log.Verbose(string.Format("Help = false, entering GetSwgohResponse()"));
                await GetSwgohReponse(turnContext, cancellationToken, messageText);
                //await turnContext.SendActivityAsync(MessageFactory.Text($"{reply.ToString()}"), cancellationToken);

            }
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text($"I'll give info about the characters from Star Wars Galaxy of Heroes! Try 'help' for help"), cancellationToken);
                }
            }
        }

        private static async Task GetSwgohReponse(ITurnContext turnContext, CancellationToken cancellationToken, string command)
        {
            Log.Verbose(string.Format("Inside GetSwgohResponse(), command text = {0}", command));
            
            List<SwgohChar> charlist;
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri("https://swgoh.gg");
                    client.DefaultRequestHeaders.Add("User-Agent", "Anything");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    HttpResponseMessage response = await client.GetAsync("/api/characters/");
                    response.EnsureSuccessStatusCode();
                    charlist = response.Content.ReadAsAsync<List<SwgohChar>>().Result;
                    string responseBody = await response.Content.ReadAsStringAsync();
                }
            } catch (Exception ex)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"Error: " + ex.Message ), cancellationToken);
                return;
            }

            List<SwgohChar> requestedchars = new List<SwgohChar>();

            if (command.StartsWith("list") && command.Contains("light"))
            {
                requestedchars.AddRange(charlist.Where(c => c.Alignment == "Light Side"));
                await SendSimpleList(turnContext, cancellationToken, requestedchars);
            }
            else if (command.StartsWith("list") && command.Contains("dark"))
            {
                requestedchars.AddRange(charlist.Where(c => c.Alignment == "Dark Side"));
                await SendSimpleList(turnContext, cancellationToken, requestedchars);
            }
            else if (command == "list" || (command.StartsWith("list") && command.Contains("all")))
            {
                requestedchars = charlist;
            }
            else if (command.StartsWith("show"))
            {
                Log.Verbose(string.Format("show = true, command text = {0}", command));
                
                string charname = command.Substring(5);
                charname.Trim();
                Log.Verbose(string.Format("show = true, stripped charname = {0}", charname));
                bool result = charname.Equals("embo");
                Log.Verbose(string.Format("String comparison = {0}", result));
                result = charname.Equals("embo", StringComparison.Ordinal);
                Log.Verbose(string.Format("String ordinal comparison = {0}", result));
                SwgohChar t1 = charlist.FirstOrDefault(c => c.Name.ToLowerInvariant() == charname);
                if (t1 == null)
                {
                    Log.Verbose(string.Format("show = true, charname returned nothing", charname));
                    requestedchars = charlist.Where(c => c.Name.ToLowerInvariant().Contains(charname)).ToList();

                    if (requestedchars.Count == 0)
                    {
                        t1 = new SwgohChar
                        {
                            Name = "Character not found!",
                            Image = "//swgohbot.azurewebsites.net/Images/not_found_128x128.png",
                            Alignment = "Please try again",
                            Description = "type 'help' for Help",
                            Base_Id = "NOTFOUND",
                            PK = "999",
                            Categories = new string[] { "Not Found" }
                            
                        };
                    }
                }

                if (requestedchars.Count > 1)
                {
                    Log.Verbose(string.Format("Query for {0} returned more than one result", charname));
                    var reply = turnContext.Activity.CreateReply();
                    reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                    for(int i = 0; i < requestedchars.Count; i++)
                    {
                        ThumbnailCard tCard = GetThumbnailCard(requestedchars[i]);
                        reply.Attachments.Add(tCard.ToAttachment());
                    }
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                } else
                {
                    ThumbnailCard cardT = GetThumbnailCard(t1);
                    //BasicCard cardB = GetBasicCard(t1);
                    //HeroCard cardH = GetHeroCard(t1);
                    //ReceiptCard card = GetReceiptCard(t1);
                    var reply = turnContext.Activity.CreateReply();

                    reply.Attachments = new List<Attachment>
                    {
                        cardT.ToAttachment()
                    };
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                    requestedchars.Add(t1);
                }

            } else
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"Unknown Command, try again"), cancellationToken);
            }
        }

        private static async Task SendSimpleList(ITurnContext turnContext, CancellationToken cancellationToken, List<SwgohChar> requestedchars)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text(GetResponseString(requestedchars)), cancellationToken);
        }

        private static string GetResponseString(List<SwgohChar> resultList)
        {
            int size = resultList.Count;
            StringBuilder responseString = new StringBuilder();
            for (int i = 0; i < size; i++)
            {
                responseString.Append("Name: " + resultList[i].Name);
                responseString.AppendLine();
                responseString.Append("Description: " + resultList[i].Description);
                responseString.AppendLine();
                responseString.Append("Light or Dark: " + resultList[i].Alignment);
                responseString.AppendLine();
                responseString.AppendLine();
            }

            return responseString.ToString();
        }

        private static async Task SendSuggestedActionsAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var reply = turnContext.Activity.CreateReply();
            ThumbnailCard suggestionCardT = new ThumbnailCard()
            {
                Title = "SWGOH Bot Help",
                Subtitle = "Valid commands are:",
                Buttons = new List<CardAction>()
                {
                    new CardAction() { Title = "List Light", Type = ActionTypes.ImBack, Value = "list light" },
                    new CardAction() { Title = "List Dark", Type = ActionTypes.ImBack, Value = "list dark" },
                    new CardAction() { Title = "Show Darth Revan", Type = ActionTypes.ImBack, Value = "show darth revan" },
                },
            };
            reply.Attachments = new List<Attachment>
            {
                suggestionCardT.ToAttachment()
            };

            await turnContext.SendActivityAsync(reply, cancellationToken);
        }

        private static BasicCard GetBasicCard(SwgohChar theChar)
        {
            var basicCard = new BasicCard
            {
                Title = theChar.Name,
                Subtitle = theChar.Base_Id,
                Text = theChar.Description,
                Images = new List<CardImage>
                {
                    new CardImage(url: "https:" + theChar.Image, alt: theChar.Base_Id)
                }
            };
            return basicCard;
        }

        private static ThumbnailCard GetThumbnailCard(SwgohChar theChar)
        {

            var thumbnailCard = new ThumbnailCard
            {
                Title = theChar.Name,
                Subtitle = theChar.Alignment,
                Images = new List<CardImage>
                {
                    new CardImage(url: "http:" + theChar.Image, alt: theChar.Base_Id)
                },
                Text = theChar.Description + "\n\n" + "**_" + theChar.Categories[0] + "_**"
            };

            return thumbnailCard;
        }

        private static HeroCard GetHeroCard(SwgohChar theChar)
        {
            var heroCard = new HeroCard
            {
                Title = theChar.Name,
                Subtitle = theChar.Alignment,
                Images = new List<CardImage>
                {
                    new CardImage(url: "https:" + theChar.Image, alt: theChar.Base_Id)
                },
                Text = theChar.Description
            };

            return heroCard;
        }

        private static ReceiptCard GetReceiptCard(SwgohChar theChar)
        {
            var receiptCard = new ReceiptCard
            {
                Title = theChar.Name,
                Facts = new List<Fact> { new Fact("Description", theChar.Description), new Fact("Alignment", theChar.Alignment) },
                Items = new List<ReceiptItem>
                {
                    new ReceiptItem(
                        image: new CardImage(url: "https:" + theChar.Image)),
                },

                Buttons = new List<CardAction>
                {
                    new CardAction(
                        ActionTypes.OpenUrl,
                        "More information on SWGOH.gg",
                        "https:" + theChar.Image,
                        value: theChar.Url),
                },
            };

            return receiptCard;
        }
    }
}
