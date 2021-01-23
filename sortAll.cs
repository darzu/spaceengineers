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

        // const string destContainerName = "Sorted belly";

        // private IMyCargoContainer destContainer;
        // private IMyInventory dest;
        // temp vars
        private List<IMyTerminalBlock> tempBlocks = new List<IMyTerminalBlock>();
        private List<IMyAssembler> tempAssemblers = new List<IMyAssembler>();
        private List<MyInventoryItem> tempItems = new List<MyInventoryItem>();
        private List<MyProductionItem> tempQueueItems = new List<MyProductionItem>();

        // private StringBuilder dynamicContent = new StringBuilder();
        // private StringBuilder asmworking = new StringBuilder();

        private List<IMyCargoContainer> sortedContainers = new List<IMyCargoContainer>();
        public void FindSortedContainers()
        {
            sortedContainers.Clear();
            GridTerminalSystem.GetBlocksOfType(
                sortedContainers, block => block.CustomName.StartsWith("store"));
        }

        private List<IMyTerminalBlock> sourceContainers = new List<IMyTerminalBlock>();
        public void FindSourceContainers()
        {
            // GridTerminalSystem.SearchBlocksOfName("belly", lst, block => block.CustomName != destContainerName);
            sourceContainers.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(sourceContainers, block =>
            {
                if (!block.HasInventory)
                    return false;
                if (block.GetInventory()?.ItemCount <= 0)
                    return false;
                return true;
            });
        }

        private List<IMyAssembler> allAssemblers = new List<IMyAssembler>();
        public void FindAllAssemblers()
        {
            allAssemblers.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyAssembler>(allAssemblers, block => true);
        }

        private IMyAssembler? leadAssembler;
        public void FindLeadAssembler()
        {
            tempAssemblers.Clear();
            tempAssemblers = allAssemblers.Where(a => !a.CooperativeMode);
            leadAssembler = tempAssemblers.Count() > 0 ? tempAssemblers[0] : null;
            if (tempAssemblers.Count() > 1) {
                Echo($"Warning: multiple non-cooperative assemblers found: {String.join(', ', tempAssemblers)}");
            } else if (tempAssemblers.Count() == 0) {
                Echo($"Warning: no non-cooperative assemblers found.");
            }
        }

        private Dictionary<MyItemType, int> allAssemblerWork = new Dictionary<MyItemType, int>();
        public void UpdateAllAssemblerWork()
        {
            allAssemblerWork.Clear();
            tempQueueItems.Clear();
            foreach (var a in allAssemblers) {
                a.GetQueue(tempQueueItems);
                // TODO: continue
                // foreach (var
                // ItemId
            }
        }

        public Program()
        {
            // (called before Main)
            FindSortedContainers();
            FindSourceContainers();
            UpdateKnownItems();
            FindLeadAssembler();
            UpdateAllAssemblerWork();

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
        public MyItemType? LookupItemType(string name)
        {
            name = name.ReplaceAll("_", " ")
            MyItemType result;
            if (namesToKnownItems.TryGetValue(name, out result))
                return result;
            if (unknownNames.Contains(name))
                return null;
            // expensive lookup
            var matchingKeys = namesToKnownItems.Keys.Where(n => n.Contains(name));
            if (matchingKeys.Count() > 0)
            {
                if (matchingKeys) {
                    // warn about ambiguity
                    Echo($"Warning: '{name}' is ambiguous between: '{String.Join(', ', matchingKeys)}'.");
                }

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

        private Dictionary<MyItemType, int> neededItemCounts = new Dictionary<MyItemType, int>();
        private List<string> itemNames = new List<string>();
        private List<int> itemDesiredCount = new List<int>();
        public void DoSort()
        {
            neededItemCounts.Clear();

            // pull into each container
            foreach (var dest in sortedContainers)
            {
                var inv = dest.GetInventory();
                if (inv == null)
                    continue;

                // figure out what items we should be storing (by parsing the name)
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
                    var specifiesDesiredCount = desiredCount > 0;

                    // figure out how many we already have
                    var iType = LookupItemType(name);
                    if (iType != null) {
                        Echo($"'{dest.CustomName}' wants invalid item '{name}'");
                        continue;
                    }
                    var currentCount = inv.GetItemAmount(iType);
                    var neededCount = desiredCount - currentCount;
                    // TODO: Actually, take all we can get always
                    // if (neededCount <= 0)
                    //    continue;

                    if (neededCount > 0)
                        Echo($"'{dest.CustomName}' wants {specifiesDesiredCount ? neededCount : 'inf.'} more {iType.TypeId}");
                    else {/* taking all anyway */}

                    // search the grid
                    foreach (var src in sourceContainers)
                    {
                        var srcInv = src.GetInventory(0);
                        if (srcInv == null)
                            continue;

                        // see how many we have
                        var srcCount = srcInv.GetItemAmount(iType);
                        if (srcCount <= 0)
                            continue;

                        var transferAmount = srcCount; // Math.min(srcCount, neededCount);
                        var trasnferSuccess = srcInv.TransferItemTo(inv, iType, transferAmount);
                        if (trasnferSuccess) {
                            Echo($"Transferred {transferAmount} {name} from '{src.CustomName}' to '{dest.CustomName}'");
                            neededCount -= transferAmount;
                        } else {
                            // TODO: log transfer success / fail ? Probably too noisy.
                        }
                    }

                    // update the counts of what we need
                    if (neededCount > 0) {
                        int prevNeededCount;
                        if (neededItemCounts.TryGetValue(i.Type, out prevNeededCount))
                            neededItemCounts[i.Type] += neededCount;
                        else
                            neededItemCounts[i.Type] = neededCount;
                    }
                }
            }

            // put items we still need into production
            UpdateAllAssemblerWork();
            if (leadAssembler != null) {
                foreach (var typeAndCount in neededItemCounts.GetKeyValuePairs()) {
                    var iType = typeAndCount.Key;
                    var neededCount = typeAndCount.Value;
                    if (neededCount <= 0)
                        continue;

                    // see how many we are already producing
                    foreach (var assembler in allAssemblers) {
                    }
                }
            }
        }

        // private List<MyInventoryItem> itemList = new List<MyInventoryItem>();
        // public void FetchComponent(string name, int maxAmount)
        // {
        //     foreach (var remoteContainer in sourceContainers)
        //     {
        //         var inv = remoteContainer.GetInventory(0);
        //         itemList.Clear();
        //         inv.GetItems(itemList, item => item.Type.SubtypeId == name);
        //         if (itemList.Count > 0)
        //         {
        //             var amount = Math.Min(maxAmount, (int)itemList[0].Amount);
        //             Echo($"transferring {amount} {name} from {remoteContainer.CustomName}");
        //             inv.TransferItemTo(destContainer.GetInventory(0), itemList[0], amount);
        //             break;
        //         }
        //     }
        //     Echo("couldn't find anywhere");
        // }

        public void Main(string argument, UpdateType updateSource)
        {
            DoSort();
            // Dictionary<string, int> desiredCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            // desiredCounts["SteelPlate"] = 10000;
            // desiredCounts["Motor"] = 1000;
            // desiredCounts["InteriorPlate"] = 10000;
            // desiredCounts["ThrusterComponent"] = 1000;
            // desiredCounts["Computer"] = 1000;
            // desiredCounts["ConstructionComponent"] = 10000;
            // desiredCounts["GirderComponent"] = 1000;
            // desiredCounts["MetalGrid"] = 10000;
            // desiredCounts["SmallTube"] = 10000;
            // desiredCounts["LargeTube"] = 1000;
            // desiredCounts["Display"] = 1000;
            // desiredCounts["BulletproofGlass"] = 1000;
            // desiredCounts["ReactorComponent"] = 1000;
            // desiredCounts["PowerCell"] = 1000;
            // desiredCounts["RadioCommunicationComponent"] = 1000;
            // desiredCounts["GravityGeneratorComponent"] = 0;
            // desiredCounts["MedicalComponent"] = 0;
            // desiredCounts["SolarCell"] = 0;
            // desiredCounts["Superconductor"] = 0;
            // desiredCounts["Canvas"] = 0;

            // List<MyInventoryItem> containerContents = new List<MyInventoryItem>();
            // destContainer.GetInventory(0).GetItems(containerContents, item => true);
            // foreach (KeyValuePair<string, int> entry in desiredCounts)
            // {
            //     // see how many of this item we have
            //     List<MyInventoryItem> itemList = new List<MyInventoryItem>();
            //     destContainer.GetInventory(0).GetItems(itemList, item => item.Type.SubtypeId == entry.Key);
            //     int maxAmount = 0;
            //     foreach (MyInventoryItem i in itemList)
            //     {
            //         maxAmount += (int)i.Amount;
            //     }
            //     Echo(entry.Key);
            //     if (maxAmount < entry.Value)
            //     {
            //         Echo("needed");
            //         FetchComponent(entry.Key, entry.Value - maxAmount);
            //     }
            //     else
            //     {
            //         Echo("not needed");
            //     }
            // }
        }
    }
}
