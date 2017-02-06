using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace TibiaSpellsToJson
{
    public class Program
    {
       
        public class Spell
        {
            public string Name;
            public string Formula;
            public string[] VocationToCast;
            public string Group;
            public string Type;
            public string Cooldown;
            public string GroupCooldown;
            public int MinimunLevel;
            public int ManaCost;
            public int PriceToLearn;
            public string[] CitiesToLearn;
            public bool PremiumOnly;
            public int SoulPoints;
            public int Charges;
            public string DamageType;

            public Spell(string name, string formula, string[] vocationToCast, string group, string type, string cooldown, string groupCooldown, int minimunLevel, int manaCost, int priceToLearn, string[] citiesToLearn, bool premiumOnly, int soulPoints, int charges, string damageType)
            {
                Name = name;
                Formula = formula;
                VocationToCast = vocationToCast;
                Group = group;
                Type = type;
                Cooldown = cooldown;
                GroupCooldown = groupCooldown;
                MinimunLevel = minimunLevel;
                ManaCost = manaCost;
                PriceToLearn = priceToLearn;
                CitiesToLearn = citiesToLearn;
                PremiumOnly = premiumOnly;
                SoulPoints = soulPoints;
                Charges = charges;
                DamageType = damageType;
            }
        }

        public enum Vocation
        {
            druid,
            sorcerer,
            paladin,
            knight
        }

        public enum SpellGroup
        {
            Attack,
            Healing,
            Support
        }

        public enum SpellType
        {
            Instant,
            Rune
        }

        private static HttpClient _client;
        public static void Main(string[] args)
        {
            _client = new HttpClient();
            Console.WriteLine("Getting spell info.");
            //get all spell info
            Task<IEnumerable<Spell>> spellGatheringTask = GetSpells(null, null, null, false);
            spellGatheringTask.Wait();
        
            Console.WriteLine("Converting into json.");
            //transform into json
            string json = JsonConvert.SerializeObject(spellGatheringTask.Result, Formatting.Indented);
         
            //save
            string currentDirectory = Directory.GetCurrentDirectory();
            if (!Directory.Exists(currentDirectory + "/json"))
                Directory.CreateDirectory(currentDirectory + "/json");

            Console.WriteLine($"Saving to file at {currentDirectory}/json/spells.json");
            File.WriteAllText($"{currentDirectory}/json/spells.json",json);

            Console.WriteLine("Finished.");
            Console.ReadLine();
        }

        private static async Task<IEnumerable<Spell>> GetSpells(Vocation? vocation, SpellGroup? group, SpellType? type, bool premiumOnly)
        {
            HttpContent postContent = new FormUrlEncodedContent(new[]
               {
                new KeyValuePair<string, string>("vocation", vocation.ToString() ?? ""),
                new KeyValuePair<string, string>("group", group.ToString() ?? ""),
                new KeyValuePair<string, string>("type", type.ToString() ?? ""),
                new KeyValuePair<string, string>("premium",premiumOnly ? "yes" : "")
               });

            HttpResponseMessage response = await _client.PostAsync("http://www.tibia.com/library/?subtopic=spells", postContent);

            if(!response.IsSuccessStatusCode) return null;

            string spellListHtml = await response.Content.ReadAsStringAsync();

            if(string.IsNullOrWhiteSpace(spellListHtml)) return null;

            HtmlDocument spellListDocument = new HtmlDocument();
            spellListDocument.LoadHtml(spellListHtml);
           
            IEnumerable<HtmlNode> spellLinkNodes = spellListDocument.DocumentNode.SelectNodes("//div[@id='spells']//table//a");
          
            IEnumerable<string> spellLinks = spellLinkNodes.Select(l => l.GetAttributeValue("href", ""));

            List<Spell> spells = new List<Spell>();
            foreach (string spellLink in spellLinks)
            {
                spells.Add(await GetSpellInfo(spellLink));
            }
            response.Dispose();
            return spells;
        }

        private static async Task<Spell> GetSpellInfo(string url)
        {
           HttpResponseMessage response = await _client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                Thread.Sleep(250);
                return await GetSpellInfo(url);
            }

            string spellHtml = await response.Content.ReadAsStringAsync();
            response.Dispose();

            if (string.IsNullOrWhiteSpace(spellHtml)) return null;
            
            return ParseSpell(spellHtml);
        }

        private static Spell ParseSpell(string html)
        {
            HtmlDocument spellDocument = new HtmlDocument();
            spellDocument.LoadHtml(html);

            IEnumerable<HtmlNode> tableNodes = spellDocument.DocumentNode.SelectNodes("//tr");

            string name = tableNodes.FirstOrDefault(n => n.InnerText.Contains("Name:"))?.InnerText?.Remove(0, 5);
            string formula = tableNodes.FirstOrDefault(n => n.InnerText.Contains("Formula:"))?.InnerText?.Remove(0, 8);
            string[] vocationToCast = tableNodes.FirstOrDefault(n => n.InnerText.Contains("Vocation:"))?
                                                           .InnerText?.Remove(0, 9)?
                                                           .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

            string group = tableNodes.FirstOrDefault(n => n.InnerText.Contains("Group:"))?.InnerText?.Remove(0, 6);
            string type = tableNodes.FirstOrDefault(n => n.InnerText.Contains("Type:"))?.InnerText?.Remove(0, 5);
            string damageType = tableNodes.FirstOrDefault(n => n.InnerText.Contains("Damage Type:"))?.InnerText?.Remove(0, 12);
            string[] cooldowns = tableNodes.FirstOrDefault(n => n.InnerText.Contains("Cooldown:"))?
                                           .InnerText?.Remove(0, 9)?
                                           .Split(new[] { "(Group: ", ")" }, StringSplitOptions.RemoveEmptyEntries);

            string spellCooldown = cooldowns?[0];
            string groupCooldown = cooldowns?[1];
            int minimunLevel = int.Parse(tableNodes.FirstOrDefault(n => n.InnerText.Contains("Exp Lvl:"))?.InnerText?.Remove(0, 8));
            int manaCost;
            int.TryParse(tableNodes.FirstOrDefault(n => n.InnerText.Contains("Mana:"))?.InnerText?.Remove(0, 5), out manaCost);
            int priceToLearn;
            int.TryParse(tableNodes.FirstOrDefault(n => n.InnerText.Contains("Price:"))?.InnerText?.Remove(0, 6), out priceToLearn);
            string[] citiesToLearn = tableNodes.FirstOrDefault(n => n.InnerText.Contains("City:"))?
                                               .InnerText?.Remove(0, 5)?
                                               .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
            bool premiumOnly = tableNodes.FirstOrDefault(n => n.InnerText.Contains("Premium:"))?.InnerText?.Remove(0, 8) == "yes";

            int soulPoints;
            int charges;
            int.TryParse(tableNodes.FirstOrDefault(n => n.InnerText.Contains("Soul Points:"))?.InnerText?.Remove(0, 12), out soulPoints);
            int.TryParse(tableNodes.FirstOrDefault(n => n.InnerText.Contains("Amount:"))?.InnerText?.Remove(0, 7), out charges);
            

            return new Spell(name, formula, vocationToCast, group, type, spellCooldown, groupCooldown, minimunLevel, manaCost, priceToLearn, citiesToLearn, premiumOnly, soulPoints, charges,damageType);
        }
    }
}
