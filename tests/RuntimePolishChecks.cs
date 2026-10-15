using System;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;
public static class RuntimePolishChecks
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Invoke(object value, string method) { value.GetType().GetMethod(method, Private).Invoke(value, null); }
    static void Set(object value, string field, object data) { value.GetType().GetField(field, Private).SetValue(value, data); }
    static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Event.current.type = EventType.KeyDown; Invoke(app, "Update"); Invoke(app, "OnGUI"); Check(Event.current.type == EventType.Used, "Handled keys cannot also activate native IMGUI focus"); GUI.commands.Clear(); Input.keys.Clear(); Event.current.type = EventType.Repaint; }
    public static void Run(FrontierRuntime app)
    {
        string previous = Application.persistentDataPath; string directory = Path.Combine(Path.GetTempPath(), "starfall-save-check-" + Guid.NewGuid()); Application.persistentDataPath = directory;
        try
        {
            var talk = new GameCore(CampaignFixture.Create());
            talk.NewGame(0); talk.Close();
            var first = new WorldObject { id = "qa-first", kind = "innGuest",
                name = "First Guest", en = "First Guest", x = 10, y = 10 };
            var second = new WorldObject { id = "qa-second", kind = "innGuest",
                name = "Second Guest", en = "Second Guest", x = 12, y = 10 };
            talk.Talk(first);
            Check(talk.conversation != null &&
                object.ReferenceEquals(talk.conversationSource, first),
                "NPC cue belongs to the actual conversation target");
            talk.save.x = second.x; talk.save.y = second.y;
            Check(object.ReferenceEquals(talk.conversationSource, first),
                "Moving toward a same-kind NPC does not transfer the cue");
            talk.Close();
            Check(talk.conversationSource == null, "Closing dialogue clears cue");
            talk.Talk(second);
            Check(talk.conversation != null &&
                object.ReferenceEquals(talk.conversationSource, second),
                "New dialogue selects the new resident");
            talk.NewGame(1);
            Check(talk.conversationSource == null,
                "Loading a fresh game clears transient NPC identity");
            Console.WriteLine("PASS NPC dialogue identity, proximity changes and reset");
            // Test the real world draw call, not only the Core pointer.
            app.Game.NewGame(0); app.Game.Close(); app.Game.Talk(first);
            Check(app.Game.conversation != null,
                "Runtime NPC fixture opened a real conversation");
            GUI.textureCalls.Clear();
            typeof(FrontierRuntime).GetMethod("DrawNpc", Private).Invoke(
                app, new object[] { "innGuest", new Point(first.x, first.y), -1 });
            Check(GUI.textureCalls.FindAll(t => t.texture != null &&
                t.texture.name == "resource:Frontier/Art/CombatRhythmRemaster").Count == 1,
                "The actual speaking NPC receives exactly one authored sigil");
            GUI.textureCalls.Clear();
            typeof(FrontierRuntime).GetMethod("DrawNpc", Private).Invoke(
                app, new object[] { "innGuest", new Point(second.x, second.y), -1 });
            Check(GUI.textureCalls.FindAll(t => t.texture != null &&
                t.texture.name == "resource:Frontier/Art/CombatRhythmRemaster").Count == 0,
                "A same-kind bystander never receives another NPC's cue");
            app.Game.Close();
            Console.WriteLine("PASS World NPC render identity for interlocutor and bystander");

            var g = new GameCore(CampaignFixture.Create()); g.NewGame(1); g.Close(); g.save.coins = 123; g.save.flags.Add("测试\\\"存档"); g.PrepareSave();
            Check(SaveStore.Write(g.save, 0), "First save write"); g.save.coins = 456; Check(SaveStore.Write(g.save, 0), "Atomic replacement write");
            string warning; var loaded = SaveStore.Read(0, out warning); Check(loaded.coins == 456 && warning == null && loaded.flags.Contains("测试\\\"存档"), "Actual production save store roundtrips payload and Unicode");
            var envelope = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(SaveStore.PathFor(0))); envelope.checksum = "tampered"; File.WriteAllText(SaveStore.PathFor(0), JsonUtility.ToJson(envelope));
            loaded = SaveStore.Read(0, out warning); Check(loaded.coins == 123 && warning != null, "Checksum failure recovers the previous backup");
            File.WriteAllText(SaveStore.PathFor(1) + ".tmp", File.ReadAllText(SaveStore.PathFor(0) + ".bak")); loaded = SaveStore.Read(1, out warning); Check(loaded.coins == 123 && warning != null, "Interrupted save recovers a verified temporary file");
            g.save.coins = 700; Check(SaveStore.Write(g.save, 1), "Independent manual slot"); g.save.coins = 800; Check(SaveStore.Write(g.save), "Separate automatic slot"); Check(SaveStore.Read(1, out warning).coins == 700 && SaveStore.Read(out warning).coins == 800, "Manual and automatic files stay independent");
            File.WriteAllText(SaveStore.PathFor(2), JsonUtility.ToJson(g.save)); Check(SaveStore.Read(2, out warning).profession == 1, "Legacy raw JSON remains readable");
            File.WriteAllText(SaveStore.PathFor(2), "{}"); Check(SaveStore.Read(2, out warning) == null && warning != null, "Incomplete metadata cannot become a fabricated save");
            string before = File.ReadAllText(SaveStore.PathFor(1)); g.save.inventory = null; Check(!SaveStore.Write(g.save, 1) && File.ReadAllText(SaveStore.PathFor(1)) == before, "Invalid serialization leaves the prior save untouched");
            Console.WriteLine("PASS Save store checksum, Unicode, backups, interrupted writes, independent slots and legacy JSON (offline serializer)");
            app.Game.Enter(0); app.Game.Close(); app.Game.save.flags.Add("weapon_1"); app.Game.save.flags.Add("core_1"); app.Game.save.weapon = 0;
            app.Game.mode = "inventory"; Set(app, "inspectedWeapon", 1); Set(app, "inspectedStyle", 1); Invoke(app, "OnGUI"); GUI.commands.Clear();
            var navigation = (MenuNavigation)typeof(FrontierRuntime).GetField("navigation", Private).GetValue(app); navigation.Select("710:580:174:52"); Key(app, KeyCode.Return); Check(app.Game.save.weapon == 1 && app.Game.Style == 1, "Keyboard focus activates the actual equipment button");
            Key(app, KeyCode.Tab); Check(app.Game.mode == "inventory", "Tab cycles menu focus without replacing its screen");
            app.Game.save.tea = 2; app.Game.save.energy = 0; navigation.Select("947:590:246:35"); Key(app, KeyCode.JoystickButton0); Check(app.Game.save.tea == 1 && app.Game.save.energy == 100 && app.Game.mode == "inventory", "A uses the selected inventory consumable without triggering a dodge");
            app.Game.mode = "pause"; Invoke(app, "OnGUI"); Key(app, KeyCode.Tab); Check(app.Game.mode == "pause", "Tab retains the pause screen");
            app.Game.mode = "saves"; Invoke(app, "OnGUI"); Key(app, KeyCode.Tab); Check(app.Game.mode == "saves", "Tab retains the save manager");
            Set(app, "saveConfirmation", "read"); Invoke(app, "OnGUI"); Key(app, KeyCode.I); Check(app.Game.mode == "saves", "Direct menu shortcuts cannot bypass confirmation"); Key(app, KeyCode.Escape);
            app.Game.mode = "journal"; app.Game.OpenCodex(0); Set(app, "dialogParent", "journal"); Invoke(app, "OnGUI"); Key(app, KeyCode.Escape); Check(app.Game.mode == "journal", "Closing a journal record returns to its parent");
            app.Game.save.flags.Add("history_0"); app.Game.OpenCodex(0); Set(app, "dialogParent", "journal"); Invoke(app, "OnGUI"); typeof(FrontierRuntime).GetMethod("ChooseDialogue", Private).Invoke(app, new object[] { 0 }); Check(app.Game.mode == "journal" && typeof(FrontierRuntime).GetField("dialogParent", Private).GetValue(app) == null, "Confirming a record clears its temporary return context");
            app.Game.mode = "title"; Set(app, "replacePrompt", true); Invoke(app, "OnGUI"); GUI.commands.Clear(); Check(navigation.CanActivate("414:447:214:43") && !navigation.CanActivate("64:595:252:56"), "New-game confirmation traps focus in the dialog"); Key(app, KeyCode.JoystickButton1); Check(!(bool)typeof(FrontierRuntime).GetField("replacePrompt", Private).GetValue(app), "Controller B cancels the modal");
            app.Game.mode = "world"; Key(app, KeyCode.Escape); Check(app.Game.mode == "pause", "Escape pauses actual gameplay"); Key(app, KeyCode.JoystickButton1); Check(app.Game.mode == "world", "Controller B returns to adventure");
            Set(app, "settingsParent", "title"); app.Game.mode = "saves"; Key(app, KeyCode.Escape); Check(app.Game.mode == "pause", "Title saves return to settings"); Key(app, KeyCode.Escape); Check(app.Game.mode == "title", "Title settings retain their return destination");
            Console.WriteLine("PASS Runtime keyboard/controller buttons, modal focus, menu return and paused world");
            var audio = (FrontierAudio)typeof(FrontierRuntime).GetField("audioSystem", Private).GetValue(app); var effects = (AudioSource)typeof(FrontierAudio).GetField("effects", Private).GetValue(audio); var ambience = (AudioSource)typeof(FrontierAudio).GetField("ambience", Private).GetValue(audio); var music = (AudioSource[])typeof(FrontierAudio).GetField("music", Private).GetValue(audio);
            app.Game.mode = "world"; app.Game.save.music = true; app.Game.save.musicVolume = .2f; app.Game.save.effectsVolume = .6f; app.Game.save.ambienceVolume = .8f; app.Game.sounds.Clear(); effects.heard.Clear(); app.Game.sound = "hit"; app.Game.sound = "hit"; app.Game.sound = "quest"; audio.Update(app.Game, false, 2);
            Check(effects.heard.Count == 2 && effects.heard.Contains("hit") && effects.heard.Contains("quest") && Math.Abs(effects.volume - .18f) < .001f && Math.Abs(ambience.volume - .48f) < .001f && music[0].volume + music[1].volume > 0, "Distinct cues survive together while repeated same-frame hits are bounded and channel volumes stay independent");
            app.Game.save.music = false; audio.Update(app.Game, false, 2); Check(effects.volume == 0 && ambience.volume == 0 && !ambience.isPlaying && !music[0].isPlaying && !music[1].isPlaying, "Sound switch mutes and stops all looping channels");
            Console.WriteLine("PASS Runtime sound channel volumes, mute and multiple-event delivery (offline sources)");
        }
        finally { Application.persistentDataPath = previous; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
