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
            int merged = 0;
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
                // aggregate Associated Quests from winning override and any earlier instances of the Dialog Item
                var dialogItemLink = dialogItem.FormKey.ToLink<IDialogTopicGetter>();
                ISet<FormKey> quests = new HashSet<FormKey>();
                IList<DialogTopicAssociatedQuest> allQuests = new List<DialogTopicAssociatedQuest>();
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
                }
                // Push Associated Quests not already present in winning override into a new override
                if (quests.Count > dialogItem.AssociatedQuests.Count)
                {
                    if (state.PatchMod.DialogTopics.TryGetOrAddAsOverride(dialogItem.ToLink(), state.LinkCache, out var updated))
                    {
                        updated.AssociatedQuests.AddRange(allQuests);
                        ++merged;
                    }
                    else
                    {
                        Console.WriteLine("Failed to add override for {0}", dialogItem);
                    }
                }
            }
            Console.WriteLine("Total Dialog Topics {0}, skipped {1}, checked masters/total records/Associated-Quests merged {2}/{3}/{4}",
                dialogItems, skipped, needsCheck, dialogItemOverrides, merged);
        }
    }
}
