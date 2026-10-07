namespace WildTamers.EditorTools
{
    /// <summary>
    /// Every UI text of the game in English and Arabic (the "UI" string table). The Arabic is written for Saudi players
    /// (clear Fusha, short and natural), not translated word for word, and uses no diacritics so every letter joins and shows correctly.
    /// {0}, {1} are filled in by code (Smart Strings, plurals included). Re-run Wild Tamers/Build/Localization after editing.
    /// </summary>
    public static class UiStrings
    {
        public struct Entry
        {
            public readonly string Key, En, Ar;
            public Entry(string key, string en, string ar) { Key = key; En = en; Ar = ar; }
        }

        public static readonly Entry[] All =
        {
            // ---------- Common ----------
            new Entry("common.level", "Lv. {0}", "مستوى {0}"),
            new Entry("common.hp", "HP {0}/{1}", "الصحة {0}/{1}"),
            new Entry("common.xp", "+{0} XP", "+{0} خبرة"),
            new Entry("common.percent", "{0}%", "{0}٪"),
            new Entry("common.close", "Close", "إغلاق"),
            new Entry("common.back", "Back", "رجوع"),
            new Entry("common.done", "Done", "تم"),
            new Entry("list.join", "{0}, {1}", "{0}، {1}"),
            new Entry("list.and", "{0} and {1}", "{0} و{1}"),
            new Entry("stat.hp", "HP", "الصحة"),
            new Entry("stat.atk", "ATK", "الهجوم"),
            new Entry("stat.def", "DEF", "الدفاع"),
            new Entry("stat.spd", "SPD", "السرعة"),

            // ---------- Main menu ----------
            new Entry("menu.title", "Wild Tamers", "مروضو البرية"),
            new Entry("menu.play", "Play", "العب"),
            new Entry("menu.team", "Team", "الفريق"),
            new Entry("menu.settings", "Settings", "الإعدادات"),
            new Entry("menu.quit", "Quit", "خروج"),

            // ---------- Pause menu ----------
            new Entry("pause.title", "Paused", "إيقاف مؤقت"),
            new Entry("pause.resume", "Resume", "متابعة"),
            new Entry("pause.settings", "Settings", "الإعدادات"),
            new Entry("pause.mainmenu", "Main Menu", "القائمة الرئيسية"),

            // ---------- Settings ----------
            new Entry("settings.title", "Settings", "الإعدادات"),
            new Entry("settings.language", "Language", "اللغة"),
            new Entry("settings.lang.en", "English", "English"),
            new Entry("settings.lang.ar", "العربية", "العربية"),
            new Entry("settings.music", "Music", "الموسيقى"),
            new Entry("settings.sfx", "Sound effects", "المؤثرات الصوتية"),
            new Entry("settings.reset", "Reset save", "مسح التقدم"),
            new Entry("settings.resetDone", "Save reset. Fresh start!", "تم مسح التقدم. بداية جديدة!"),

            // ---------- Confirm popups ----------
            new Entry("confirm.reset.title", "Reset save?", "مسح التقدم؟"),
            new Entry("confirm.reset.body",
                "This deletes all your animals and starts a new game.\nThis can't be undone.",
                "سيتم حذف كل حيواناتك وبدء لعبة جديدة.\nلا يمكن التراجع عن ذلك."),
            new Entry("confirm.reset.yes", "Yes, reset", "نعم، امسح"),
            new Entry("confirm.cancel", "Cancel", "إلغاء"),
            new Entry("confirm.leave.title", "Leave the battle?", "ترك المعركة؟"),
            new Entry("confirm.leave.body",
                "Your team will retreat.\nThe wild animal stays where it is.",
                "سيتراجع فريقك.\nويبقى الحيوان البري في مكانه."),
            new Entry("confirm.leave.yes", "Yes, leave", "نعم، غادر"),

            // ---------- Map ----------
            new Entry("hud.team", "TEAM", "الفريق"),
            new Entry("hud.hint",
                "WASD or click to walk  •  Scroll to zoom  •  Q/E to turn",
                "تحرك بمفاتيح WASD أو بالنقر  •  قرب بعجلة الفأرة  •  دوّر بمفتاحي Q/E"),
            new Entry("toast.closer", "Get closer!  ({0} m to go)", "اقترب أكثر!  بقي {0} م"),
            new Entry("toast.ranaway", "It ran away!", "لقد هرب!"),
            new Entry("toast.joined", "{0} joined your team!", "انضم {0} إلى فريقك!"),
            new Entry("toast.healed", "Your team rested and is fully healed.", "استراح فريقك وتعافى تماما."),
            new Entry("toast.escaped", "Your team got away safely!", "تمكن فريقك من الهرب بسلام!"),

            // ---------- Encounter ----------
            new Entry("encounter.title", "Wild boss encounter!", "ظهر زعيم بري!"),
            new Entry("encounter.leave", "Leave", "انسحاب"),
            new Entry("encounter.fight", "Fight!", "قاتل!"),

            // ---------- Team screen ----------
            new Entry("team.title", "Your Team", "فريقك"),
            new Entry("team.count", "{0:plural:{} animal|{} animals}", "الحيوانات: {0}"),
            new Entry("team.footer",
                "Tap an animal to read its story.\nYou pick your fighters before every battle.",
                "المس أي حيوان لتقرأ قصته.\nوتختار مقاتليك قبل كل معركة."),
            new Entry("team.badge", "TEAM", "الفريق"),
            new Entry("team.info", "INFO", "معلومات"),

            // ---------- Team select ----------
            new Entry("select.title", "Choose your team", "اختر فريقك"),
            new Entry("select.foe", "{0}  •  Lv. {1}", "{0}  •  مستوى {1}"),
            new Entry("select.generic", "Pick your fighters", "اختر مقاتليك"),
            new Entry("select.pick", "Pick {0:plural:{} animal|{} animals} for this fight.",
                "اختر {0:plural:{} حيوان|حيوانا واحدا|حيوانين|{} حيوانات|{} حيوانا|{} حيوان} لهذه المعركة."),
            new Entry("select.short", "Only {0:plural:{} animal can|{} animals can} fight right now.",
                "لا يستطيع القتال الآن سوى {0:plural:{} حيوان|حيوان واحد|حيوانين|{} حيوانات|{} حيوان|{} حيوان}."),
            new Entry("select.fight", "Fight!", "قاتل!"),
            new Entry("select.more", "Pick {0} more", "اختر {0:plural:{} آخر|حيوانا آخر|حيوانين آخرين|{} حيوانات أخرى|{} حيوانا آخر|{} حيوان آخر}"),
            new Entry("select.fainted", "FAINTED", "مغشي عليه"),
            new Entry("select.resting", "RESTING", "يستريح"),

            // ---------- Animal card ----------
            new Entry("card.new", "NEW ANIMAL!", "حيوان جديد!"),
            new Entry("card.info", "ANIMAL INFO", "معلومات الحيوان"),
            new Entry("card.about", "ABOUT", "نبذة"),
            new Entry("card.history", "HISTORY", "التاريخ"),
            new Entry("card.awesome", "Awesome!", "رائع!"),

            // ---------- Battle: cards and captions ----------
            new Entry("battle.wildtag", "WILD BOSS", "زعيم بري"),
            new Entry("battle.guard", "GUARD", "دفاع"),
            new Entry("battle.turn", "TURN", "الدور"),
            new Entry("battle.fainted", "FAINTED", "مغشي عليه"),
            new Entry("battle.crit", "CRITICAL!", "ضربة حرجة!"),
            new Entry("battle.guardcaption", "GUARD", "دفاع"),

            // ---------- Battle: action buttons ----------
            new Entry("act.attack", "Attack", "هجوم"),
            new Entry("act.skill", "Skill", "مهارة"),
            new Entry("act.defend", "Defend", "دفاع"),
            new Entry("act.run", "Run", "هروب"),
            new Entry("act.defend.hint", "Half damage", "نصف الضرر"),
            new Entry("act.skill.next", "Ready next turn", "جاهزة في الدور القادم"),
            new Entry("act.skill.in", "Ready in {0:plural:{} turn|{} turns}",
                "جاهزة بعد {0:plural:{} دور|دور واحد|دورين|{} أدوار|{} دورا|{} دور}"),
            new Entry("act.run.hint", "{0}% team escape", "فرصة الهرب {0}٪"),
            new Entry("act.default.attack", "Tackle", "ضربة"),

            // ---------- Battle: log ----------
            new Entry("log.appeared", "A wild <b>{0}</b> appeared!", "ظهر <b>{1}</b>!"),
            new Entry("log.go", "Go, {0}!", "إلى المعركة: {0}!"),
            new Entry("log.what", "What will <b>{0}</b> do?", "ماذا سيفعل <b>{0}</b>؟"),
            new Entry("log.guarding", "<b>{0}</b> is guarding!", "<b>{0}</b> يتخذ وضع الدفاع!"),
            new Entry("log.used", "<b>{0}</b> used <b>{1}</b>!", "<b>{0}</b> يستخدم <b>{1}</b>!"),
            new Entry("log.usedon", "<b>{0}</b> used <b>{1}</b> on <b>{2}</b>!", "<b>{0}</b> يستخدم <b>{1}</b> على <b>{2}</b>!"),
            new Entry("log.damage", "<color=#EF476F>{0} damage</color>", "<color=#EF476F>ضرر {0}</color>"),
            new Entry("log.crit", "<color=#FF8A3D>Critical hit!</color>", "<color=#FF8A3D>ضربة حرجة!</color>"),
            new Entry("log.guarded", "<color=#4DA3FF>Guarded:</color>", "<color=#4DA3FF>تم الصد:</color>"),
            new Entry("log.fainted", "<b>{0}</b> fainted!", "سقط <b>{0}</b>!"),
            new Entry("log.tryrun", "Your team tries to run…", "يحاول فريقك الهرب…"),
            new Entry("log.gotaway", "Your team got away safely!", "تمكن فريقك من الهرب بسلام!"),
            new Entry("log.cantrun", "Couldn't get away from the <b>{0}</b>!", "لم تتمكن من الهرب من <b>{0}</b>!"),
            new Entry("log.joined", "<b>{0}</b> joined your team!", "انضم <b>{0}</b> إلى فريقك!"),
            new Entry("log.xp", "Everyone gained <b>{0} XP</b>!", "حصل الجميع على <b>{0} خبرة</b>!"),
            new Entry("log.wipe", "Your whole team fainted…", "سقط فريقك بالكامل…"),
            new Entry("log.noanimals", "No animals to battle.", "لا توجد حيوانات للقتال."),

            // ---------- Battle: result sheet ----------
            new Entry("result.victory", "Victory!", "انتصار!"),
            new Entry("result.joined", "<b>{0}</b> (Lv. {1}) joined your team!", "انضم <b>{0}</b> (مستوى {1}) إلى فريقك!"),
            new Entry("result.won", "You won!", "لقد فزت!"),
            new Entry("result.xp", "Each animal of your team gets +{0} XP.", "ينال كل حيوان في فريقك {0} نقطة خبرة."),
            new Entry("result.defeat", "Defeated…", "هزيمة…"),
            new Entry("result.defeat.all", "Your whole team fainted.\nIt rested and is fully healed.", "سقط فريقك بالكامل.\nلكنه استراح وتعافى تماما."),
            new Entry("result.defeat.one", "<b>{0}</b> fainted.\nYour team rested and is fully healed.", "سقط <b>{0}</b>.\nواستراح فريقك وتعافى تماما."),
            new Entry("result.continue", "Continue", "متابعة"),
            new Entry("result.levelup", "LEVEL UP!", "مستوى جديد!"),
        };
    }
}
