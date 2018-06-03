using System;
using System.Text;
using TShockAPI;
using Terraria;
using TerrariaApi.Server;
using System.Net;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace StreamAuth
{
    [ApiVersion(2, 1)]
    public class StreamAuth : TerrariaPlugin
    {
        public override string Author => "OneBadPanda";
        public override string Description => "Connects StreamElements Points system to Terraria";
        public override string Name => "StreamAuth";
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
            Commands.ChatCommands.Add(new Command(VIPMe, "vipme"));
            Commands.ChatCommands.Add(new Command(TrustMe, "trustme"));
            // All hooks provided by TSAPI are a part of the _ServerApi_ namespace.
            // This example will show you how to use the ServerChat hook which is 
            // fired whenever a client sends a message to the server.
            // In order to register the hook you need to pass in the class that 
            // is registering the hook and it's callback function (OnServerChat)
            // By passing a reference to the `OnServerChat` method you are able to
            // execute code whenever a message is sent to the server.
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);

            // TShockAPI.Hooks.PlayerHooks.PlayerCommand override /login <playername> to playername.tolower()


            // This is an example of subscribing to TShock's TogglePvP event.
            // This event is a part of the `GetDataHandlers` class.
            // All events located within this class are _packet implementation_ hooks.
            // These hooks will come in handy when dealing with packets
            // because they provide the packet's full structure, saving you from
            // reading the packet data yourself.

        }
        public void VIPMe(CommandArgs args)
        {
            //check group
            if (args.Player.Group == TShock.Groups.GetGroupByName("TrustedPlayer"))
            {
                var points = StreamElementsPointsRequest(args);
                //check points
                if (points.points >= 200)
                {

                    Console.WriteLine(args.Player.Group.Name);
                    var postpoints = StreamElementsSetPoints(args, -200);
                    if (points.points > postpoints.newAmount)
                    {
                        TShock.Users.SetUserGroup(args.Player.User, "vip");
                    }
                    else
                    {
                        Console.WriteLine("Previous: " + points.points + " Post: " + postpoints.newAmount);
                    }
                }
            }
            else
            {
                args.Player.SendInfoMessage("You must be in the TrustedPlayer group and have 200 shoots to redeem this");
            }
        }
        public void TrustMe(CommandArgs args)
        {

            //check group
            if (args.Player.Group.Name.Trim() == TShock.Config.DefaultGuestGroupName)
            {
                //check points
                var points = StreamElementsPointsRequest(args);
                if (points.points >= 200)
                {
                    var postpoints = StreamElementsSetPoints(args, -200);
                    if (points.points > postpoints.newAmount)
                    {
                        TShock.Users.SetUserGroup(args.Player.User, "TrustedPlayer");
                    }
                    else
                    {
                        Console.WriteLine("Something went wrong... " + args.Player.User.Name + "Previous: " + points.points + " Post: " + postpoints);
                    }
                }
                else
                {
                    args.Player.SendInfoMessage("You must be in the Default group and have 200 shoots to redeem this");
                }
            }
            else
            {
                args.Player.SendInfoMessage("You must be in the Default group and have 200 shoots to redeem this");
            }
                            
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // As mentioned in the Hello, World! tutorial you should always 
                // deregister your hooks during the disposal process.
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
            }
            base.Dispose(disposing);
        }
        void OnServerJoin(JoinEventArgs args)
        {
            StreamElementsCheckJoinRedemptions(null);
            Console.WriteLine("checking join redemptions");
        }
        // This is the ServerChat's callback function; this function is called
        // whenever the ServerChat hook is fired, which is upon receiving a message
        // from the client.
        // This example acts as a debug and outputs the message to the console.
        public void Points(CommandArgs args)
        {
            var userPoints = StreamElementsPointsRequest(args);
            args.Player.SendMessage(msg: ("Account: " + userPoints.username + " Points: " + userPoints.points + " Rank: " + userPoints.rank), red: 255, green: 255, blue: 255);
        }

        public void StreamElementsCheckJoinRedemptions(CommandArgs args)
        {
            int limit = 40;
            bool pending = true;
            string queryParams = "?limit=" + limit + "&pending=" + pending;
            //grab list
            Models.RootObject list = GetStoreRedemptions(queryParams, null);
            if (list.docs.Count != 0)
            {
                //make account
                //mark redemption complete 
                CreateAccount(list);
            }
            else
            {}
        }
        
        void MarkStreamElementsRedemptionComplete(Models.Doc Doc)
        {
            string jwtToken = Models.StreamElements.jwtToken;
            string channel = Models.StreamElements.channel;
            Uri uri = new Uri("https://api.streamelements.com/kappa/v2/" + "store" + "/" + channel + "/" + "redemptions" + "/" + Doc._id);
            HttpWebRequest request = (HttpWebRequest)WebRequest.CreateDefault(uri);
            request.Headers["Authorization"] = ("Bearer " + jwtToken);
            request.Method = "PUT";
            request.ContentType = "application/json; charset=utf-8";
            request.Accept = "Accept=application/json";
            string serializedObject = JsonConvert.SerializeObject(new
            { completed = true });

            request.SendChunked = false;
            request.ContentLength = serializedObject.Length;
            using (var writer = new StreamWriter(request.GetRequestStream()))
            {
                writer.Write(serializedObject);
            }
            var response = request.GetResponse() as HttpWebResponse;
            response.Close();
        }
        public Models.RootObject GetStoreRedemptions(string queryParams, string itemID)
        {
            string jwtToken = Models.StreamElements.jwtToken;
            string channel = Models.StreamElements.channel;
            if (queryParams == null)
            { queryParams = "?completed=true"; }
            else
            {
                queryParams += "&completed=true";
            }
            Uri urlAddress = new Uri("https://api.streamelements.com/kappa/v2/" + "store" + "/" + channel + "/" + "redemptions" + "/" + itemID + queryParams);
            HttpWebRequest request = (HttpWebRequest)WebRequest.CreateDefault(urlAddress);
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
        
        public Models.Points.UserPoints StreamElementsPointsRequest(CommandArgs args)
        {

            string user = string.Join(" ", args.Parameters);
            if (string.IsNullOrWhiteSpace(user))
            {
                user = args.Player.User.Name;
                if (string.IsNullOrWhiteSpace(user))
                {
                    return null;
                }
            }
            string jwtToken = Models.StreamElements.jwtToken;
            string channel = Models.StreamElements.channel;
            Uri urlAddress = new Uri("https://api.streamelements.com/kappa/v2/" + "points" + "/" + channel + "/" + user);
            HttpWebRequest request = (HttpWebRequest)WebRequest.CreateDefault(urlAddress);
            request.Headers["Authorization"] = ("Bearer " + jwtToken);
            ServicePointManager.ServerCertificateValidationCallback = MyRemoteCertificateValidationCallback;
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Stream receiveStream = response.GetResponseStream();
                StreamReader readStream = null;
                if (response.CharacterSet == null)
                {
                    readStream = new StreamReader(receiveStream);
                }
                else
                {
                    readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                }
                // Send Message to player
                Models.Points.UserPoints userPoints = ParsePointsObjectFromStream(response.GetResponseStream());
                response.Close();
                readStream.Close();
                return userPoints;
            }
            return null;

        }
        public Models.Points.PutPointsResponse StreamElementsSetPoints(CommandArgs args, int points)
        {

            string user = string.Join(" ", args.Parameters);
            if (string.IsNullOrWhiteSpace(user))
            {
                user = args.Player.User.Name;
                if (string.IsNullOrWhiteSpace(user))
                {
                    return null;
                }
            }
            string jwtToken = Models.StreamElements.jwtToken;
            string channel = Models.StreamElements.channel;
            Uri urlAddress = new Uri("https://api.streamelements.com/kappa/v2/" + "points" + "/" + channel + "/" + user + "/" + points);
            HttpWebRequest request = (HttpWebRequest)WebRequest.CreateDefault(urlAddress);
            request.Headers["Authorization"] = ("Bearer " + jwtToken);
            request.Method = "PUT";
            request.ContentType = "application/json; charset=utf-8";
            request.Accept = "Accept=application/json";
            request.SendChunked = false;
            var response = request.GetResponse() as HttpWebResponse;
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Stream receiveStream = response.GetResponseStream();
                StreamReader readStream = null;
                if (response.CharacterSet == null)
                {
                    readStream = new StreamReader(receiveStream);
                }
                else
                {
                    readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                }
                // Send Message to player
                Models.Points.PutPointsResponse userPoints = ParsePutPointsObjectFromStream(response.GetResponseStream());
                response.Close();
                readStream.Close();
                args.Player.SendMessage(msg: ("Account: " + userPoints.username + " Points: " + userPoints.newAmount), red: 255, green: 255, blue: 255);
                return userPoints;
            }
            return null;
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
            for (int i = 0; i <= (a.docs.Count - 1); i++)
            {
                var Doc = a.docs[i];
                if (Doc.item.ToString().Contains("Join"))
                {
                    var account = new TShockAPI.DB.User();
                    account.Name = Doc.redeemer.username;
                    account.Group = TShock.Config.DefaultGuestGroupName;
                    string password = String.Join(" ", Doc.input.ToArray());
                    //Check for account already
                    if (TShock.Users.GetUserByName(account.Name) == null)
                    {
                        Console.WriteLine(account.Name + "User Does Not Exist");
                        //make the account
                        TShock.Users.AddUser(account);
                    }
                    //assign the password
                    var regex = new Regex("^[a-zA-Z0-9]*$");
                    if (password.Length > TShock.Config.MinimumPasswordLength)
                    {
                        if (regex.IsMatch(password))
                        {
                            TShock.Users.SetUserPassword(account, password);
                            Console.WriteLine(account.Name + " has changed their password.");
                        }
                        else
                        {
                            Console.WriteLine("Password was shit.  Please try alphanumerical only.");
                        }
                    }
                    // Mark the redemption complete.
                    MarkStreamElementsRedemptionComplete(Doc);
                }
                else
                {
                    Console.WriteLine("Redemption list does not contain Join.");
                }
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
        public static Models.Doc ParseDocObjectFromStream(Stream stream)
        {
            string value;
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                value = reader.ReadToEnd();
            }
            Models.Doc RedepmtionList = JsonConvert.DeserializeObject<Models.Doc>(value);
            return RedepmtionList;
        }
        public static Models.Points.UserPoints ParsePointsObjectFromStream(Stream stream)
        {
            string value;
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                value = reader.ReadToEnd();
            }
            Models.Points.UserPoints RedepmtionList = JsonConvert.DeserializeObject<Models.Points.UserPoints>(value);
            return RedepmtionList;
        }
        public static Models.Points.PutPointsResponse ParsePutPointsObjectFromStream(Stream stream)
        {
            string value;
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                value = reader.ReadToEnd();
            }
            Models.Points.PutPointsResponse RedepmtionList = JsonConvert.DeserializeObject<Models.Points.PutPointsResponse>(value);
            return RedepmtionList;
        }
        /* UNDER CONSTRUCTION
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
        }*/
    }
}