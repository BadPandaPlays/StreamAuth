using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;
using Terraria;
using TerrariaApi.Server;
using System.Net;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Terraria.ID;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace StreamAuth
{
    [ApiVersion(2, 1)]
    public class StreamAuth : TerrariaPlugin
    {
        public override string Author => "OneBadPanda";
        public override string Description => "Uses StreamElements Store to Create SSC Terraria Accounts";
        public override string Name => "StreamStore";
        public override Version Version
        {
            get { return new Version(1, 0, 0, 0); }
        }

        public StreamAuth(Main game) : base(game)
        {

        }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(Points, "points"));
            Commands.ChatCommands.Add(new Command(StreamElementsCheckJoinRedemptions, "join"));
            // All hooks provided by TSAPI are a part of the _ServerApi_ namespace.
            // This example will show you how to use the ServerChat hook which is 
            // fired whenever a client sends a message to the server.
            // In order to register the hook you need to pass in the class that 
            // is registering the hook and it's callback function (OnServerChat)
            // By passing a reference to the `OnServerChat` method you are able to
            // execute code whenever a message is sent to the server.
            // This is an example of subscribing to TShock's TogglePvP event.
            // This event is a part of the `GetDataHandlers` class.
            // All events located within this class are _packet implementation_ hooks.
            // These hooks will come in handy when dealing with packets
            // because they provide the packet's full structure, saving you from
            // reading the packet data yourself.
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // As mentioned in the Hello, World! tutorial you should always 
                // deregister your hooks during the disposal process.
            }
            base.Dispose(disposing);
        }

        // This is the ServerChat's callback function; this function is called
        // whenever the ServerChat hook is fired, which is upon receiving a message
        // from the client.
        // This example acts as a debug and outputs the message to the console.
        public void Points(CommandArgs args)
        {
            StreamElementsPointsRequest(args);
        }

        public void StreamElementsCheckJoinRedemptions(CommandArgs args)
        {
            int limit = 40;
            bool pending = true;
            string queryParams = "?limit=" + limit + "&pending=" + pending;
            Models.RootObject list = GetStoreRedemptions(args, queryParams);
            if (list.docs.Count == 0)
            {
                args.Player.SendInfoMessage("There were no join requests.");
            }
            //create account
            else
            {
                CreateAccount(list);
            };
        }

        public static void BuyItem(CommandArgs args)
        {

            // check points
            // change amount to max stack or most points amount
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}buy <item name/id> [item amount]", Commands.Specifier);
                return;
            }
            int amountParamIndex = -1;
            int itemAmount = 0;
            for (int i = 1; i < args.Parameters.Count; i++)
            {
                if (int.TryParse(args.Parameters[i], out itemAmount))
                {
                    amountParamIndex = i;
                    break;
                }
            }
            string itemNameOrId;
            if (amountParamIndex == -1)
                itemNameOrId = string.Join(" ", args.Parameters);
            else
                itemNameOrId = string.Join(" ", args.Parameters.Take(amountParamIndex));
            Item item;
            List<Item> matchedItems = TShock.Utils.GetItemByIdOrName(itemNameOrId);
            if (matchedItems.Count == 0)
            {
                args.Player.SendErrorMessage("Invalid item type!");
                return;
            }
            else if (matchedItems.Count > 1)
            {
                args.Player.SendErrorMessage("More than one match found: ");
                var lines = PaginationTools.BuildLinesFromTerms(matchedItems.ToArray().Select(i => $"{i.Name}({i.netID})"));
                lines.ForEach(args.Player.SendInfoMessage);
                return;
            }
            else
            {
                item = matchedItems[0];
            }
            if (item.type < 1 && item.type >= Main.maxItemTypes)
            {
                args.Player.SendErrorMessage("The item type {0} is invalid.", itemNameOrId);
                return;
            }

            int prefixId = 0;
            if (amountParamIndex != -1 && args.Parameters.Count > amountParamIndex + 1)
            {
                string prefixidOrName = args.Parameters[amountParamIndex + 1];
                var prefixIds = TShock.Utils.GetPrefixByIdOrName(prefixidOrName);

                if (item.accessory && prefixIds.Contains(PrefixID.Quick))
                {
                    prefixIds.Remove(PrefixID.Quick);
                    prefixIds.Remove(PrefixID.Quick2);
                    prefixIds.Add(PrefixID.Quick2);
                }
                else if (!item.accessory && prefixIds.Contains(PrefixID.Quick))
                    prefixIds.Remove(PrefixID.Quick2);

                if (prefixIds.Count > 1)
                {
                    args.Player.SendErrorMessage("More than one match found: ");
                    var lines = (prefixIds.Select(p => p.ToString())).ToArray();
                    lines.ForEach(args.Player.SendInfoMessage);
                    args.Player.SendErrorMessage("Use \"my query\" for items with spaces.");
                    return;
                }
                else if (prefixIds.Count == 0)
                {
                    args.Player.SendErrorMessage("No prefix matched \"{0}\".", prefixidOrName);
                    return;
                }
                else
                {
                    prefixId = prefixIds[0];
                }
            }

            if (args.Player.InventorySlotAvailable || (item.type > 70 && item.type < 75) || item.ammo > 0 || item.type == 58 || item.type == 184)
            {
                if (itemAmount == 0 || itemAmount > item.maxStack)
                    itemAmount = item.maxStack;

                if (args.Player.GiveItemCheck(item.type, TShockAPI.Localization.EnglishLanguage.GetItemNameById(item.type), item.width, item.height, itemAmount, prefixId))
                {
                    item.prefix = (byte)prefixId;
                    args.Player.SendSuccessMessage("Gave {0} {1}(s).", itemAmount, item.AffixName());
                }
                else
                {
                    args.Player.SendErrorMessage("You cannot spawn banned items.");
                }
            }
            else
            {
                args.Player.SendErrorMessage("Your inventory seems full.");
            }
        }
        
        public Models.RootObject GetStoreRedemptions(CommandArgs args, string queryParams)
        {
            string jwtToken = Models.StreamElements.jwtToken;
            string channel = Models.StreamElements.channel;
            string urlAddress = "https://api.streamelements.com/kappa/v2/" + "store" + "/" + channel + "/" + "redemptions" + "/" + queryParams;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlAddress);
            request.Headers["Authorization"] = ("Bearer " + jwtToken);

            ServicePointManager.ServerCertificateValidationCallback = MyRemoteCertificateValidationCallback;
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            if (response.StatusCode == HttpStatusCode.OK)
            {
                Models.RootObject data = ParseRootObjectFromStream(response.GetResponseStream());
                response.Close();
                return data;
            }

            else
            {
                Console.WriteLine("HttpStatusCode is not OK:" + response.StatusCode.ToString());
                return null;
            }

        }

        public void StreamElementsPointsRequest(CommandArgs args)
        {

            string user = string.Join(" ", args.Parameters);
            if (string.IsNullOrWhiteSpace(user))
            {
                user = args.Player.Name;
            }
            string jwtToken = Models.StreamElements.jwtToken;
            string channel = Models.StreamElements.channel;
            string urlAddress = "https://api.streamelements.com/kappa/v2/" + "points" + "/" + channel + "/" + user;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlAddress);
            request.Headers["Authorization"] = ("Bearer " + jwtToken);
            ServicePointManager.ServerCertificateValidationCallback = MyRemoteCertificateValidationCallback;
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Stream receiveStream = response.GetResponseStream();
                var data = ParseObjectFromStream(receiveStream);
                response.Close();
                string message = data.username + " | Points: " + data.points + " | rank: " + data.rank;
                args.Player.SendMessage(message, 255, 255, 255);
            }
        }

        public static Models.RootObject ParseRootObjectFromStream(Stream stream)
        {
            string value;
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                value = reader.ReadToEnd();
            }
            Models.RootObject RedepmtionList = JsonConvert.DeserializeObject<Models.RootObject>(value);
            return RedepmtionList;
        }

        public static Models.Points.UserPoints ParseObjectFromStream(Stream stream)
        {
            string value;
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                value = reader.ReadToEnd();
            }
            var PointsObject = JsonConvert.DeserializeObject<Models.Points.UserPoints>(value);
            return PointsObject;
        }

        private bool MyRemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            bool isOk = true;
            // If there are errors in the certificate chain, look at each error to determine the cause.
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                for (int i = 0; i < chain.ChainStatus.Length; i++)
                {
                    if (chain.ChainStatus[i].Status != X509ChainStatusFlags.RevocationStatusUnknown)
                    {
                        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                        chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                        bool chainIsValid = chain.Build((X509Certificate2)certificate);
                        if (!chainIsValid)
                        {
                            isOk = false;
                        }
                    }
                }
            }
            return isOk;
        }

        public void CreateAccount(Models.RootObject a)
        {
            for (int i = 0; i < (a.docs.Count - 1); i++)
            {
                var b = a.docs[i];
                if (b.item.ToString().Contains("Join"))
                {
                    var account = new TShockAPI.DB.User();
                    account.Name = b.redeemer.username;
                    account.Group = TShock.Config.DefaultGuestGroupName;
                    string password = String.Join(" ", b.input.ToArray());
                    //Check for account already
                    if (TShock.Users.GetUserByName(account.Name) == null)
                    {
                        Console.WriteLine(account.Name +"User Does Not Exist");
                        //make the account
                        var regex = new Regex("^[a-zA-Z0-9]*$");
                        if (b.input.ToString().Length > TShock.Config.MinimumPasswordLength)
                        {
                            if (regex.IsMatch(password))
                            {
                                TShock.Users.AddUser(account);
                                TShock.Users.SetUserPassword(account, password);
                                Console.WriteLine(account.Name + " has changed their password.");
                            }
                            else
                            {
                                Console.WriteLine("Password contained special characters:  Must be alpha-numerical only.");
                            }
                        }
                        
                    }
                    MarkStreamElementsRedemptionComplete(b);
                }
                else {
                    Console.WriteLine("Redemption list does not contain Join.");
                }
            }
            
        }

        void MarkStreamElementsRedemptionComplete(Models.Doc b)
        {
            string jwtToken = Models.StreamElements.jwtToken;
            string channel = Models.StreamElements.channel;
            Uri uri = new Uri("https://api.streamelements.com/kappa/v2/" + "store" + "/" + channel + "/" + "redemptions" + "/" + b._id);
            HttpWebRequest request = (HttpWebRequest)WebRequest.CreateDefault(uri);
            request.Headers["Authorization"] = ("Bearer " + jwtToken);
            request.Method = "PUT";
            byte[] array = {34,99,111,109,112,108,101,116,101,100,34,58,32,116,114,117,101 };

            request.ContentType = "application/json; charset=utf-8";
            request.Accept = "Accept=application/json";
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(array, 0, array.Length);
            dataStream.Close();
            var response = request.GetResponse() as HttpWebResponse;
            response.Close();
        }
    }
}