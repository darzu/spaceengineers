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
        // temp vars
        private List<IMyTerminalBlock> tempBlocks = new List<IMyTerminalBlock>();
        private List<IMyAssembler> tempAssemblers = new List<IMyAssembler>();
        private List<MyInventoryItem> tempItems = new List<MyInventoryItem>();
        private List<MyProductionItem> tempQueueItems = new List<MyProductionItem>();

        private List<IMyCargoContainer> sortedContainers = new List<IMyCargoContainer>();
        public void FindSortedContainers()
        {
            // Echo("dbg: FindSortedContainers()");
            sortedContainers.Clear();
            GridTerminalSystem.GetBlocksOfType(
                sortedContainers, block => block.IsWorking && block.CustomName.StartsWith("store"));
            // sortedContainers.ForEach(s => Echo($"dbg: Found store: '{s.CustomName}'"));
        }

        private List<IMyTerminalBlock> sourceContainers = new List<IMyTerminalBlock>();
        public void FindSourceContainers()
        {
            // Echo("dbg: FindSourceContainers()");
            sourceContainers.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(sourceContainers, block =>
            {
                if (!block.HasInventory)
                    return false;
                if (block.CustomName.StartsWith("store"))
                    return false;
                if (!block.IsWorking)
                    return false;
                return true;
            });
            // sourceContainers.ForEach(s => Echo($"dbg: Found src: '{s.CustomName}'"));
        }

        private List<IMyAssembler> allAssemblers = new List<IMyAssembler>();
        public void FindAllAssemblers()
        {
            // Echo("dbg: FindAllAssemblers()");
            allAssemblers.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyAssembler>(allAssemblers, block => block.IsWorking);
        }

        private IMyAssembler leadAssembler;
        public void FindLeadAssembler()
        {
            // Echo("dbg: FindLeadAssembler()");
            tempAssemblers.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyAssembler>(tempAssemblers, a => a.IsWorking && !a.CooperativeMode);
            leadAssembler = tempAssemblers.Count() > 0 ? tempAssemblers[0] : null;
            if (tempAssemblers.Count() > 1)
            {
                Echo($"Warning: multiple non-cooperative assemblers found: {String.Join(", ", tempAssemblers)}");
            }
            else if (tempAssemblers.Count() == 0)
            {
                Echo($"Warning: no non-cooperative assemblers found.");
                return;
            }
            Echo($"Lead assembler is: {leadAssembler.CustomName}.");
        }

        private Dictionary<MyItemType, int> allAssemblerWork = new Dictionary<MyItemType, int>();
        public void UpdateAllAssemblerWork()
        {
            // Echo("dbg: UpdateAllAssemblerWork()");
            tempQueueItems.Clear();
            allAssemblerWork.Clear();
            foreach (var a in allAssemblers)
            {
                a.GetQueue(tempQueueItems);
                foreach (var qi in tempQueueItems)
                {
                    var iType = LookupItemType(qi.BlueprintId.SubtypeName.Replace("Component", ""));
                    if (!iType.HasValue)
                    {
                        Echo($"Warning: cannot lookup item type for assembler queue item: {qi.BlueprintId.TypeId.ToString()}/{qi.BlueprintId.SubtypeName}");
                        continue;
                    }
                    int count;
                    if (allAssemblerWork.TryGetValue(iType.Value, out count))
                        allAssemblerWork[iType.Value] += (int)qi.Amount;
                    else
                    {
                        Echo($"dbg: found: {qi.BlueprintId.TypeId}/{qi.BlueprintId.SubtypeName}");
                        allAssemblerWork[iType.Value] = (int)qi.Amount;
                    }
                }
            }
        }


        private Dictionary<string, MyItemType> namesToKnownItems = new Dictionary<string, MyItemType>(StringComparer.OrdinalIgnoreCase);
        // private Dictionary<uint, MyItemType> typeIdToKnownItems = new Dictionary<uint, MyItemType>();
        private HashSet<string> unknownNames = new HashSet<string>();
        private Dictionary<MyItemType, int> allItemCounts = new Dictionary<MyItemType, int>();
        public void UpdateKnownItems()
        {
            // Echo("dbg: UpdateKnownItems()");
            namesToKnownItems.Clear();
            allItemCounts.Clear();
            // typeIdToKnownItems.Clear();
            tempBlocks.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(tempBlocks, block => block.IsWorking && block.HasInventory);
            foreach (var block in tempBlocks)
            {
                for (var invIdx = 0; invIdx < block.InventoryCount; invIdx++)
                {
                    var inv = block.GetInventory(invIdx);
                    if (inv == null)
                        continue;
                    tempItems.Clear();
                    inv.GetItems(tempItems);
                    foreach (var i in tempItems)
                    {
                        namesToKnownItems[i.Type.SubtypeId] = i.Type;
                        int count;
                        if (allItemCounts.TryGetValue(i.Type, out count))
                        {
                            allItemCounts[i.Type] += (int)i.Amount;
                        }
                        else
                        {
                            // Echo($"Found new type: {i.Type.TypeId}/{i.Type.SubtypeId}");
                            allItemCounts[i.Type] = (int)i.Amount;
                        }
                    }
                }
            }

            foreach (var t in allItemCounts.Keys)
            {
                // Echo($"dbg: {t.SubtypeId}-{allItemCounts[t]}");
                // TODO: display to terminal
            }
        }
        public MyItemType? LookupItemType(string name)
        {
            name = name.Replace("_", "");
            MyItemType result;
            if (namesToKnownItems.TryGetValue(name, out result))
                return result;
            if (unknownNames.Contains(name))
                return null;
            // expensive lookup
            var matchingKeys = namesToKnownItems.Keys.Where(n => n.ToLower().Contains(name.ToLower()));
            if (matchingKeys.Count() > 0)
            {
                if (matchingKeys.Count() > 1)
                {
                    // warn about ambiguity
                    Echo($"Warning: '{name}' is ambiguous between: '{String.Join(", ", matchingKeys)}'.");
                }

                // found
                result = namesToKnownItems[matchingKeys.First()];
                namesToKnownItems[name] = result;
                return result;
            }
            else
            {
                unknownNames.Add(name);
            }
            return null;
        }

        public MyDefinitionId? FindBlueprint(MyItemType iType)
        {
            MyDefinitionId blueprint;
            if (MyDefinitionId.TryParse($"MyObjectBuilder_BlueprintDefinition/{iType.SubtypeId}", out blueprint))
            {
                if (leadAssembler.CanUseBlueprint(blueprint))
                {
                    return blueprint;
                }
            }

            if (MyDefinitionId.TryParse($"MyObjectBuilder_BlueprintDefinition/{iType.SubtypeId}Component", out blueprint))
            {
                if (leadAssembler.CanUseBlueprint(blueprint))
                {
                    return blueprint;
                }
            }

            return null;
        }

        private Dictionary<MyItemType, int> neededItemCounts = new Dictionary<MyItemType, int>();
        private List<string> itemNames = new List<string>();
        private List<int> itemDesiredCount = new List<int>();
        public void DoSort()
        {
            // Echo("dbg: DoSort()");
            neededItemCounts.Clear();

            // pull into each container
            foreach (var dest in sortedContainers)
            {
                //Echo($"dbg: sorting {dest.CustomName}");
                var inv = dest.GetInventory();
                if (inv == null)
                {
                    Echo($"Warning: Destination '{dest.CustomName}' doesn't have an inventory.");
                    continue;
                }

                // figure out what items we should be storing (by parsing the name)
                var nameWords = dest.CustomName.Split().Skip(1/*"store"*/);
                //Echo($"dbg: nameWords: {String.Join(" ", nameWords)}");
                itemNames.Clear();
                itemDesiredCount.Clear();
                foreach (var word in nameWords)
                {
                    if (word.Length == 0)
                        continue;
                    var parts = word.Split('-');
                    var name = parts[0];
                    var count = 0;
                    if (parts.Length == 2)
                        int.TryParse(parts[1], out count);
                    //Echo($"dbg: item name {name}-{count}");
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
                    // Echo($"dbg: we want {name}-{desiredCount}");

                    // figure out how many we already have
                    var iType = LookupItemType(name);
                    if (!iType.HasValue)
                    {
                        Echo($"Warning: '{dest.CustomName}' wants invalid item '{name}'. All known names: {String.Join(",", namesToKnownItems.Keys)}");
                        continue;
                    }
                    var currentCount = inv.GetItemAmount(iType.Value);
                    // Echo($"dbg: we have {currentCount}");
                    int currentGlobalCount = 0;
                    allItemCounts.TryGetValue(iType.Value, out currentGlobalCount);
                    var producingCount = 0;
                    allAssemblerWork.TryGetValue(iType.Value, out producingCount);
                    var neededCount = desiredCount - currentCount;
                    // TODO: Actually, take all we can get always
                    // if (neededCount <= 0)
                    //    continue;

                    if (neededCount > 0)
                    {
                        Echo($"We want {desiredCount} {name} and we have {currentCount} locally and {currentGlobalCount - currentCount} more globally and we are making {producingCount}.");
                        // Echo($"'{dest.CustomName}' wants {(specifiesDesiredCount ? neededCount.ToString() : "inf.")} more {iType.Value.SubtypeId}");
                    }
                    else {/* taking all anyway */}

                    // search the grid
                    foreach (var src in sourceContainers)
                    {
                        // for each inventory
                        for (var invIdx = 0; invIdx < src.InventoryCount; invIdx++)
                        {
                            var srcInv = src.GetInventory(invIdx);
                            if (srcInv == null)
                                continue;

                            // see how many we have
                            var srcCount = srcInv.GetItemAmount(iType.Value);
                            if (srcCount <= 0)
                                continue;

                            // Echo($"dbg: {src.CustomName} has {srcCount}");

                            // transfer each occurrance
                            tempItems.Clear();
                            srcInv.GetItems(tempItems, item => item.Type.SubtypeId == iType.Value.SubtypeId);
                            foreach (var invI in tempItems)
                            {
                                var trasnferSuccess = srcInv.TransferItemTo(inv, invI, invI.Amount);
                                if (trasnferSuccess)
                                {
                                    Echo($"Transferred {invI.Amount} {name} from '{src.CustomName}'");
                                    neededCount -= invI.Amount;
                                }
                                else
                                {
                                    Echo($"dbg: Transferring {invI.Amount} {name} from '{src.CustomName}' to '{dest.CustomName} failed.");
                                    break;
                                    // TODO: log transfer success / fail ? Probably too noisy.
                                }
                            }
                        }
                    }

                    // update the counts of what we need
                    if (neededCount > 0)
                    {
                        //Echo($"dbg: we didn't get enough {name}; we need {neededCount} more");
                        int prevNeededCount;
                        if (neededItemCounts.TryGetValue(iType.Value, out prevNeededCount))
                            neededItemCounts[iType.Value] += (int)neededCount;
                        else
                            neededItemCounts[iType.Value] = (int)neededCount;
                    }
                }
            }

            // put items we still need into production
            // UpdateAllAssemblerWork(); // see how many we are already producing
            if (leadAssembler != null)
            {
                foreach (var typeAndCount in neededItemCounts)
                {
                    var iType = typeAndCount.Key;
                    var wantedCount = typeAndCount.Value;
                    if (wantedCount <= 0)
                        continue;
                    // Echo($"dbg: We want {wantedCount} more {iType.SubtypeId}");

                    // compare to what we are producing currently
                    var producingCount = 0;
                    allAssemblerWork.TryGetValue(iType, out producingCount);
                    var neededCount = wantedCount - producingCount;
                    // Echo($"dbg: We need {neededCount} more {iType.SubtypeId}");
                    if (neededCount <= 0)
                    {
                        // Echo($"We are already trying to produce {producingCount}.");
                        continue;
                    } else
                    {
                        // Echo($"dbg: We are producing any.");
                    }

                    // enqueue
                    MyDefinitionId? blueprintOpt = FindBlueprint(iType);
                    if (!blueprintOpt.HasValue)
                    {
                        Echo($"Warning: Failed to find definition for {iType.SubtypeId}");
                        continue;
                    }
                    MyDefinitionId blueprint = blueprintOpt.Value;

                    try
                    {
                        leadAssembler.AddQueueItem(blueprint, (MyFixedPoint)neededCount);
                        if (allAssemblerWork.ContainsKey(iType))
                            allAssemblerWork[iType] += neededCount;
                        else
                            allAssemblerWork[iType] = neededCount;
                        Echo($"Enqueued {wantedCount} more {blueprint.SubtypeName}");
                    } catch (Exception e)
                    {
                        Echo($"Warning: Lead assembler failed to produce {blueprint.TypeId}/{blueprint.SubtypeName}");
                    }
                }
            }
        }

        // Super Sorter Out 1

        public Program() // called before Main
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            FindSortedContainers();
            FindSourceContainers();
            UpdateKnownItems();
            FindAllAssemblers();
            UpdateAllAssemblerWork();
            FindLeadAssembler();

            DoSort();
        }
    }
}
