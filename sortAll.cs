using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    { 
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        const string destContainerName = "Sorted belly";

        private IMyCargoContainer destContainer;
        private IMyInventory dest;
        private List<IMyTerminalBlock> sourceContainers;
        private List<IMyTerminalBlock> tempBlocks;
        private List<MyInventoryItem> tempItems;
        private StringBuilder dynamicContent = new StringBuilder();
        private StringBuilder asmworking = new StringBuilder();

        private List<IMyCargoContainer> sortedContainers = new List<IMyCargoContainer>();

        public void FindSortedContainers()
        {
            sortedContainers.Clear();
            GridTerminalSystem.GetBlocksOfType(
                sortedContainers, block => block.CustomName.StartsWith("store"));
        }

        public void FindSourceContainers()
        {
            // GridTerminalSystem.SearchBlocksOfName("belly", lst, block => block.CustomName != destContainerName);
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(sourceContainers, block =>
            {
                if (!block.HasInventory)
                    return false;
                if (block.GetInventory()?.ItemCount <= 0)
                    return false;
                return true;
            });
        }

        public Program()
        {
            // init (called before main)
            FindSortedContainers();
            FindSourceContainers();
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        private Dictionary<string, MyItemType> namesToKnownItems = new Dictionary<string, MyItemType>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> unknownNames = new HashSet<string>();
        private Dictionary<MyItemType, int> allItemCounts = new Dictionary<MyItemType, int>();
        public void UpdateKnownItems()
        {
            namesToKnownItems.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(tempBlocks, block => block.HasInventory);
            foreach (var block in tempBlocks)
            {
                var inv = block.GetInventory();
                if (inv == null)
                    continue;
                inv.GetItems(tempItems);
                foreach (var i in tempItems)
                {
                    namesToKnownItems[i.Type.TypeId] = i.Type;
                    int count;
                    if (allItemCounts.TryGetValue(i.Type, out count))
                    {
                        allItemCounts[i.Type] += (int)i.Amount;
                    } else
                    {
                        allItemCounts[i.Type] = (int)i.Amount;
                    }
                }
            }
        }
        public MyItemType? LookupItem(string name)
        {
            MyItemType result;
            if (namesToKnownItems.TryGetValue(name, out result))
                return result;
            if (unknownNames.Contains(name))
                return null;
            // expensive lookup
            var matchingKey = namesToKnownItems.Keys.FirstOrDefault(n => n.Contains(name));
            if (matchingKey != "" && matchingKey != null)
            {
                // found
                result = namesToKnownItems[matchingKey];
                namesToKnownItems[name] = result;
                return result;
            }
            else
            {
                unknownNames.Add(name);
            }
            return null;
        }

        public void DoSort()
        {
            List<string> itemNames = new List<string>();
            List<int> itemDesiredCount = new List<int>();
            // pull into each container
            foreach (var dest in sortedContainers)
            {
                var inv = dest.GetInventory();
                if (inv == null)
                    continue;

                // figure out what items we should be storing
                var nameWords = dest.CustomName.Split().Skip(1/*"store"*/);
                itemNames.Clear();
                itemDesiredCount.Clear();
                foreach (var word in nameWords)
                {
                    if (word.Length == 0)
                        continue;
                    var parts = word.Split('-');
                    var name = parts[0];
                    var count = 0;
                    int.TryParse(parts[1], out count);
                    itemNames.Add(name);
                    itemDesiredCount.Add(count);
                }
                if (itemNames.Count() == 0)
                    continue;

                // for each item...
                for (var i = 0; i < itemNames.Count(); i++)
                {
                    var name = itemNames[i];
                    var desiredCount = itemDesiredCount[i];

                    // figure out how many we already have
                    inv.get
                }

                // search the grid
                foreach (var src in sourceContainers)
                {

                }
            }
        }

        private List<MyInventoryItem> itemList = new List<MyInventoryItem>();
        public void FetchComponent(string name, int maxAmount)
        {
            foreach (var remoteContainer in sourceContainers)
            {
                var inv = remoteContainer.GetInventory(0);
                itemList.Clear();
                inv.GetItems(itemList, item => item.Type.SubtypeId == name);
                if (itemList.Count > 0)
                {
                    var amount = Math.Min(maxAmount, (int)itemList[0].Amount);
                    Echo($"transferring {amount} {name} from {remoteContainer.CustomName}");
                    inv.TransferItemTo(destContainer.GetInventory(0), itemList[0], amount);
                    break;
                }
            }
            Echo("couldn't find anywhere");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Dictionary<string, int> desiredCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            desiredCounts["SteelPlate"] = 10000;
            desiredCounts["Motor"] = 1000;
            desiredCounts["InteriorPlate"] = 10000;
            desiredCounts["ThrusterComponent"] = 1000;
            desiredCounts["Computer"] = 1000;
            desiredCounts["ConstructionComponent"] = 10000;
            desiredCounts["GirderComponent"] = 1000;
            desiredCounts["MetalGrid"] = 10000;
            desiredCounts["SmallTube"] = 10000;
            desiredCounts["LargeTube"] = 1000;
            desiredCounts["Display"] = 1000;
            desiredCounts["BulletproofGlass"] = 1000;
            desiredCounts["ReactorComponent"] = 1000;
            desiredCounts["PowerCell"] = 1000;
            desiredCounts["RadioCommunicationComponent"] = 1000;
            desiredCounts["GravityGeneratorComponent"] = 0;
            desiredCounts["MedicalComponent"] = 0;
            desiredCounts["SolarCell"] = 0;
            desiredCounts["Superconductor"] = 0;
            desiredCounts["Canvas"] = 0;

            List<MyInventoryItem> containerContents = new List<MyInventoryItem>();
            destContainer.GetInventory(0).GetItems(containerContents, item => true);
            foreach (KeyValuePair<string, int> entry in desiredCounts)
            {
                // see how many of this item we have
                List<MyInventoryItem> itemList = new List<MyInventoryItem>();
                destContainer.GetInventory(0).GetItems(itemList, item => item.Type.SubtypeId == entry.Key);
                int maxAmount = 0;
                foreach (MyInventoryItem i in itemList)
                {
                    maxAmount += (int)i.Amount;
                }
                Echo(entry.Key);
                if (maxAmount < entry.Value)
                {
                    Echo("needed");
                    FetchComponent(entry.Key, entry.Value - maxAmount);
                }
                else
                {
                    Echo("not needed");
                }
            }
        }
    }
}
