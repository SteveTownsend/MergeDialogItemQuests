using CommandLine;
using DynamicData;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout3;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using Noggog.StructuredStrings.CSharp;

namespace MergeDialogItemQuests
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<IFallout3Mod, IFallout3ModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.Fallout3, "YourPatcher.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<IFallout3Mod, IFallout3ModGetter> state)
        {
            int dialogItems = 0;
            int questsMerged = 0;
            int responsesMerged = 0;
            int needsCheck = 0;
            int dialogItemOverrides = 0;
            int skipped = 0;
            Console.WriteLine("Aggregate all unique Quests from any master or override for a Dialog Item");
            foreach (var dialogItem in state.LoadOrder.PriorityOrder.DialogTopic().WinningOverrides())
            {
                ++dialogItems;
                if (dialogItem is null)
                {
                    ++skipped;
                    continue;
                }
                ++needsCheck;
                ++dialogItemOverrides;
                // aggregate Associated Quests and Info Order (Masters) from winning override and any earlier instances of the Dialog Item
                // Info Order (Masters) is stored as Info Order (all previous)
                var dialogItemLink = dialogItem.FormKey.ToLink<IDialogTopicGetter>();
                ISet<FormKey> quests = new HashSet<FormKey>();
                IList<DialogTopicAssociatedQuest> allQuests = new List<DialogTopicAssociatedQuest>();
                ISet<FormKey> infoOrders = new HashSet<FormKey>();
                IList<IFormLinkGetter<IDialogResponsesGetter>> allInfoOrders = new List<IFormLinkGetter<IDialogResponsesGetter>>();
                bool first = true;
                foreach (var dialogItemVersion in dialogItemLink.ResolveAll(state.LinkCache))
                {
                    ++dialogItemOverrides;
                    foreach (var quest in dialogItemVersion.AssociatedQuests)
                    {
                        // No need to copy entries from winning override but we must record and count them
                        if (quests.Add(quest.Quest.FormKey) && !first)
                        {
                            allQuests.Add(quest.DeepCopy());
                        }
                    }
                    first = false;
                    if (dialogItemVersion.InfoOrderMastersOnly is null)
                    {
                        continue;
                    }
                    foreach (var info in dialogItemVersion.InfoOrderMastersOnly)
                    {
                        // No need to copy entries from winning override but we must record and count them
                        if (infoOrders.Add(info.FormKey))
                        {
                            allInfoOrders.Add(info);
                        }
                    }
                }
                // Push Associated Quests and Info Order for all previous overrides into patch, provided not already present in winning override
                if (quests.Count > dialogItem.AssociatedQuests.Count)
                {
                    if (state.PatchMod.DialogTopics.TryGetOrAddAsOverride(dialogItem.ToLink(), state.LinkCache, out var updated))
                    {
                        updated.AssociatedQuests.AddRange(allQuests);
                        ++questsMerged;
                    }
                    else
                    {
                        Console.WriteLine("Failed to add override 1 {0}", dialogItem);
                    }
                }
                if (infoOrders.Count > 0 &&
                    (dialogItem.InfoOrderAllPreviousModules is null ||
                     infoOrders.Count > dialogItem.InfoOrderAllPreviousModules.Count))
                {
                    if (state.PatchMod.DialogTopics.TryGetOrAddAsOverride(dialogItem.ToLink(), state.LinkCache, out var updated))
                    {
                        if (updated.InfoOrderAllPreviousModules is null)
                        {
                            updated.InfoOrderAllPreviousModules = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>();
                        }
                        updated.InfoOrderAllPreviousModules.AddRange(allInfoOrders);
                        ++responsesMerged;
                    }
                    else
                    {
                        Console.WriteLine("Failed to add override 2 {0}", dialogItem);
                    }
                }
            }
            Console.WriteLine("Total Dialog Topics {0}, skipped {1}, checked masters/total records {2}/{3}, Associated-Quests/Dialog-Responses merged {4}/{5}",
                dialogItems, skipped, needsCheck, dialogItemOverrides, questsMerged, responsesMerged);
        }
    }
}
