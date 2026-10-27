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
    // Executes within the registered production input harness. Forged
    // numeric identities must not mask a missing real traveler attacker.
    private static void VerifyWayfarerLethalOwnership()
    {
        var game=new GameCore(CampaignFixture.Create());
        game.NewGame(0);game.Close();
        game.save.stage=3;game.save.flags.Add("scout_saved");
        game.Enter(1);game.mode="world";
        game.enemies.Clear();
        WorldObject traveler=null;
        foreach(WorldObject actor in game.Map.objects)
            if(actor.kind=="wayfarer1"){traveler=actor;break;}
        Check(traveler!=null,"Published region has a real roadside traveler");
        game.save.x=traveler.x;game.save.y=traveler.y;
        game.Talk(traveler);
        int choice=Array.FindIndex(game.conversation.choices,
            c=>c.action=="wayfarer_defend_1");
        Check(choice>=0,"Actual traveler offers physical defense");
        game.Choose(choice);
        Check(game.WayfarerEncounterRoom==1,
            "The authored rescue spawned its real defense encounter");
        var attackers=game.enemies.FindAll(e=>e.id>=82000&&e.id<82100);
        Check(attackers.Count==2,
            "Actual rescue spawns two distinct attacker bodies");
        int coins=game.save.coins;
        var counterfeit=new Enemy(attackers[0].id,attackers[0].kind,
            attackers[0].x,attackers[0].y,25f);
        game.enemies.Add(counterfeit);
        typeof(GameCore).GetMethod("OnWayfarerEnemyDefeated",Private)
            .Invoke(game,new object[]{counterfeit});
        // Removing the real actor WITHOUT a genuine death must abort the
        // mission even when an impersonator shares its reserved numeric ID.
        game.enemies.Remove(attackers[0]);
        typeof(GameCore).GetMethod("TickWayfarerEncounter",Private)
            .Invoke(game,new object[]{.02f});
        Check(game.WayfarerEncounterRoom==0&&
            !game.save.flags.Contains("wayfarer_1")&&
            game.save.coins==coins&&game.save.version==2,
            "False same-ID lethal proof cannot finish or strand the rescue");
        // The normal physical two-kill completion path must remain valid.
        game.enemies.Remove(counterfeit);
        game.save.x=traveler.x;game.save.y=traveler.y;
        game.Talk(traveler);
        choice=Array.FindIndex(game.conversation.choices,
            c=>c.action=="wayfarer_defend_1");
        Check(choice>=0,"Aborted rescue can be retried without lost progress");
        game.Choose(choice);
        attackers=game.enemies.FindAll(e=>e.id>=82000&&e.id<82100);
        Check(attackers.Count==2,
            "Retry spawns new legitimate attacker identities");
        foreach(Enemy attacker in attackers)
        {
            typeof(GameCore).GetMethod("OnWayfarerEnemyDefeated",Private)
                .Invoke(game,new object[]{attacker});
            game.enemies.Remove(attacker);
        }
        typeof(GameCore).GetMethod("TickWayfarerEncounter",Private)
            .Invoke(game,new object[]{.02f});
        Check(game.WayfarerEncounterRoom==0&&
            game.save.flags.Contains("wayfarer_defended_1")&&
            game.save.version==2,
            "Both authentic raider deaths still complete original rescue with V2 schema");
        Console.WriteLine("PASS Wayfarer genuine Enemy ownership: fake-ID kill rejected, failed encounter retried, authentic two-kill reward accepted");
    }

        // This entrypoint is registered in the actually executed offline
        // FrontierRuntime input harness. Avoid running the unrelated legacy
        // UI/save setup above just to test room audio transitions.
        public static void RunAudio(FrontierRuntime ignored)
        {
            var game = new GameCore(CampaignFixture.Create());
            game.NewGame(0);
            game.Close();
            game.mode = "world";
            game.save.ambienceVolume = .8f;
            // Exercise the live FrontierAudio room transition, not a detached
            // math helper. Two channels must be simultaneously audible during
            // a crossing, and a quick return must preserve the old soundscape.
            var atmosphere = new FrontierAudio(new GameObject("region-audio-check"));
            try
            {
            game.mode = "world"; game.save.music = true;
            game.save.room = 0;
            atmosphere.Update(game, false, 2f);
            var roomNow = (AudioSource)typeof(FrontierAudio).GetField("ambience", Private).GetValue(atmosphere);
            Check(roomNow.clip != null && roomNow.clip.name == "ambience0" && roomNow.isPlaying,
                "Initial outdoor atmosphere is connected to the running audio system");
            game.save.room = 2;
            atmosphere.Update(game, false, .24f);
            roomNow = (AudioSource)typeof(FrontierAudio).GetField("ambience", Private).GetValue(atmosphere);
            var roomNext = (AudioSource)typeof(FrontierAudio).GetField("ambienceIncoming", Private).GetValue(atmosphere);
            float oldGain = roomNow.volume, incomingGain = roomNext.volume;
            Check(roomNow.isPlaying && roomNext.isPlaying && oldGain > 0 && incomingGain > 0 &&
                roomNext.clip.name == "ambience2",
                "Crossing a region overlaps both original and arriving outdoor audio clips");
            game.save.room = 0;
            atmosphere.Update(game, false, .12f);
            Check(roomNext.volume < incomingGain && roomNext.volume > 0,
                "Immediately returning to the old region reverses its unfinished sound crossfade");
            atmosphere.Update(game, false, 2f);
            roomNow = (AudioSource)typeof(FrontierAudio).GetField("ambience", Private).GetValue(atmosphere);
            Check(roomNow.clip.name == "ambience0" && roomNow.isPlaying && !roomNext.isPlaying,
                "Return cancels the unwanted destination soundscape without restarting the home loop");
            game.save.room = 6;
            atmosphere.Update(game, false, .96f);
            game.save.room = 7;
            atmosphere.Update(game, false, .12f);
            roomNow = (AudioSource)typeof(FrontierAudio).GetField("ambience", Private).GetValue(atmosphere);
            roomNext = (AudioSource)typeof(FrontierAudio).GetField("ambienceIncoming", Private).GetValue(atmosphere);
            Check(roomNow.clip.name == "ambience6" && roomNext.clip.name == "ambience7" &&
                roomNow.isPlaying && roomNext.isPlaying,
                "Rapid third-region transition keeps the dominant incoming landscape rather than resurrecting a quiet old room");
            atmosphere.Update(game, false, 2f);
            roomNow = (AudioSource)typeof(FrontierAudio).GetField("ambience", Private).GetValue(atmosphere);
            Check(roomNow.clip.name == "ambience7" && roomNow.isPlaying,
                "Third-region soundscape settles on its real destination");
            game.save.music = false;
            atmosphere.Update(game, false, .1f);
            Check(!roomNow.isPlaying, "Mute stops the active outdoor loop");
            game.save.music = true;
            atmosphere.Update(game, false, .1f);
            Check(roomNow.isPlaying, "Restoring audio resumes the correct new region");
            Console.WriteLine("PASS Runtime outdoor soundscapes: overlapping region transitions, reversing, retargeting and mute recovery (offline sources)");
            // The actual procedural PCM is what the looping AudioSources
            // receive. Inspect edge energy, not merely whether a generator
            // returned eight seconds of nonempty data. Old fades to silence
            // at both edges fail this regression.
            foreach (int room in new[] { 0, 2, 5, 7 })
            {
                float[] samples = AudioSynthesis.Ambience(room);
                float[] again = AudioSynthesis.Ambience(room);
                Check(samples.Length == AudioSynthesis.Rate * 8 &&
                    samples[0] == again[0] &&
                    samples[samples.Length - 1] == again[again.Length - 1],
                    "Deterministic environment PCM retains its original eight-second Unity clip length");
                double boundaryEnergy = 0, referenceEnergy = 0;
                for (int i = 0; i < AudioSynthesis.Rate / 5; i++)
                {
                    double a = samples[i], b = samples[samples.Length - 1 - i];
                    double c = samples[AudioSynthesis.Rate * 3 + i];
                    double d = samples[AudioSynthesis.Rate * 4 + i];
                    boundaryEnergy += a * a + b * b;
                    referenceEnergy += c * c + d * d;
                }
                Check(referenceEnergy > 0 && boundaryEnergy > referenceEnergy * .35 &&
                    Math.Abs(samples[0] - samples[samples.Length - 1]) < .025,
                    "Room " + room + " has an audible, continuous atmosphere at the PCM wrap without a half-second artificial fade-to-silence");
            }
            Console.WriteLine("PASS Procedural forest/coast/weather atmosphere: deterministic seamless PCM loop and stable edge loudness");
            }
            finally { atmosphere.Dispose(); }
            VerifyWayfarerLethalOwnership();
        }

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
            // The full original v1 schema is still readable without adding
            // v2 expansion fields, timestamps or an integrity envelope.
            g.save.version = 1;
            File.WriteAllText(SaveStore.PathFor(2), JsonUtility.ToJson(g.save));
            loaded = SaveStore.Read(2, out warning);
            Check(loaded != null && loaded.version == 1 && loaded.flags.Contains("测试\\\"存档"),
                "Full historical v1 raw save retains old quest flags");
            g.save.version = 2;
            // A syntactically valid truncated raw JSON was formerly accepted
            // as a new hero and could erase flags when autosaved.
            File.WriteAllText(SaveStore.PathFor(2),
                "{\"version\":2,\"room\":0,\"x\":18,\"y\":15,\"health\":100,\"coins\":25,\"inventory\":[4,2,4,0,2,0,1,3]}");
            Check(SaveStore.Read(2, out warning) == null && warning != null,
                "Partial raw save cannot silently erase quest flags or story progression");
            File.WriteAllText(SaveStore.PathFor(2), "{}"); Check(SaveStore.Read(2, out warning) == null && warning != null, "Incomplete metadata cannot become a fabricated save");
            // Invalid in-memory saves must never replace a valid manual slot.
            string before = File.ReadAllText(SaveStore.PathFor(1)); g.save.inventory = null;
            Check(!SaveStore.Write(g.save, 1) && File.ReadAllText(SaveStore.PathFor(1)) == before,
                "Invalid inventory serialization leaves the prior save untouched");
            g.save.inventory = new[] { 4, 2, 4, 0, 2, 0, 1, 3 };
            var priorFlags = g.save.flags;
            g.save.flags = null;
            Check(!SaveStore.Write(g.save, 1) && File.ReadAllText(SaveStore.PathFor(1)) == before,
                "Missing quest flags cannot replace the last good manual save");
            g.save.flags = priorFlags;
            // The recovered .tmp may be the only authentic save after a
            // power loss; an invalid new in-memory save must not truncate it
            // before serialization is checked.
            string lastGoodTemp=File.ReadAllText(SaveStore.PathFor(1));
            string recoverable=SaveStore.PathFor(2)+".tmp";
            File.WriteAllText(recoverable,lastGoodTemp);
            g.save.inventory=null;
            Check(!SaveStore.Write(g.save,2)&&
                File.ReadAllText(recoverable)==lastGoodTemp,
                "Invalid inventory cannot overwrite the only recoverable interrupted save");
            g.save.inventory=new[]{4,2,4,0,2,0,1,3};
            g.save.flags=null;
            Check(!SaveStore.Write(g.save,2)&&
                File.ReadAllText(recoverable)==lastGoodTemp,
                "Invalid quest flags cannot overwrite the only recoverable interrupted save");
            g.save.flags=priorFlags;
            loaded=SaveStore.Read(2,out warning);
            Check(loaded!=null&&loaded.coins==700&&
                warning!=null&&warning.Contains("Recovered"),
                "A corrupt primary still recovers its untouched valid .tmp after failed writes");
            Console.WriteLine("PASS Save store checksum, Unicode, backups, interrupted writes, independent slots, preflight recovery and legacy JSON (offline serializer)");
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
            var audio = (FrontierAudio)typeof(FrontierRuntime).GetField("audioSystem", Private).GetValue(app); var effects = (AudioSource)typeof(FrontierAudio).GetField("effects", Private).GetValue(audio); var music = (AudioSource[])typeof(FrontierAudio).GetField("music", Private).GetValue(audio);
            app.Game.mode = "world"; app.Game.save.music = true; app.Game.save.musicVolume = .2f; app.Game.save.effectsVolume = .6f; app.Game.save.ambienceVolume = .8f; app.Game.sounds.Clear(); effects.heard.Clear(); app.Game.sound = "hit"; app.Game.sound = "hit"; app.Game.sound = "quest"; audio.Update(app.Game, false, 2);
            var ambience = (AudioSource)typeof(FrontierAudio).GetField("ambience", Private).GetValue(audio);
            Check(effects.heard.Count == 2 && effects.heard.Contains("hit") && effects.heard.Contains("quest") && Math.Abs(effects.volume - .18f) < .001f && Math.Abs(ambience.volume - .48f) < .001f && music[0].volume + music[1].volume > 0, "Distinct cues survive together while repeated same-frame hits are bounded and channel volumes stay independent");
            app.Game.save.music = false; audio.Update(app.Game, false, 2); Check(effects.volume == 0 && ambience.volume == 0 && !ambience.isPlaying && !music[0].isPlaying && !music[1].isPlaying, "Sound switch mutes and stops all looping channels");
            Console.WriteLine("PASS Runtime sound channel volumes, mute and multiple-event delivery (offline sources)");

        }
        finally { Application.persistentDataPath = previous; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
