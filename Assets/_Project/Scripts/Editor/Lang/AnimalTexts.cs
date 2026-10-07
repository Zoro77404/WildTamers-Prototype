namespace WildTamers.EditorTools
{
    /// <summary>
    /// Names, descriptions, histories and move names of the eight animals in English and Arabic (the "Animals" string table).
    /// The Arabic was written from scratch for Saudi players, not translated from the English: clear Fusha, warm and simple,
    /// with real Arabic phrases where they fit ("سفينة الصحراء", "عيون المها", "أروغ من ثعلب"), and only real facts.
    /// No diacritics are used, so every letter joins and shows correctly.
    /// Fields: name (label), the (with "the"), wild (the wild boss), style, description, history, attack, skill, skilldesc.
    /// </summary>
    public static class AnimalTexts
    {
        public struct Texts
        {
            public string Name, The, Wild, Style, Description, History, Attack, Skill, SkillDescription;
        }

        public struct Animal
        {
            public string Id;
            public Texts En, Ar;
        }

        public static readonly Animal[] All =
        {
            new Animal
            {
                Id = "camel",
                En = new Texts
                {
                    Name = "Camel", The = "Camel", Wild = "Wild Camel", Style = "Slow & tanky",
                    Description = "The camel is a tall desert animal with one big hump. The hump stores fat, so a camel can travel a long way without food. Its wide feet walk easily on soft sand.",
                    History = "For thousands of years, people in Arabia travelled the desert on camels. Camel caravans carried frankincense, spices and trade goods between faraway towns. Bedouin families also drank camel milk and made cloth from its hair.",
                    Attack = "Stomp", Skill = "Sandstorm Slam", SkillDescription = "A heavy stomp that throws up a cloud of sand.",
                },
                Ar = new Texts
                {
                    Name = "جمل", The = "الجمل", Wild = "الجمل البري", Style = "بطيء ومتين",
                    Description = "الجمل حيوان صحراوي طويل القامة وله سنام واحد، ويسمونه \"سفينة الصحراء\". يخزن السنام الدهون، فيستطيع الجمل أن يقطع الصحراء أياما طويلة دون طعام أو ماء. وأخفافه العريضة تمنعه من الغوص في الرمال الناعمة.",
                    History = "اعتمد أهل الجزيرة العربية على الإبل آلاف السنين في الترحال وعبور الصحراء. وحملت القوافل عليها اللبان والتوابل والبضائع بين المدن البعيدة. وكان البدو يشربون حليبها وينسجون من وبرها الأغطية والحبال.",
                    Attack = "دوسة", Skill = "ضربة العجاج", SkillDescription = "دوسة ثقيلة تثير سحابة من الرمال.",
                },
            },
            new Animal
            {
                Id = "arabian_horse",
                En = new Texts
                {
                    Name = "Arabian Horse", The = "Arabian Horse", Wild = "Wild Arabian Horse", Style = "Fast & enduring",
                    Description = "The Arabian horse is one of the oldest horse breeds in the world. It has a proud high tail, a curved face and lots of stamina. Arabians are quick, smart and gentle with people.",
                    History = "Bedouin families in Arabia carefully raised Arabian horses for many centuries. The horses carried riders across the desert on long journeys, and mares were cherished almost like family. Today, Arabian blood helps make many other horse breeds fast and strong.",
                    Attack = "Kick", Skill = "Desert Gallop", SkillDescription = "A thundering charge across the sand.",
                },
                Ar = new Texts
                {
                    Name = "حصان عربي", The = "الحصان العربي", Wild = "الحصان العربي البري", Style = "سريع وشديد التحمل",
                    Description = "الحصان العربي من أقدم سلالات الخيل في العالم. له رأس صغير ووجه مقعر وذيل مرفوع بفخر، ويتحمل الجري مسافات طويلة. وهو سريع وذكي ولطيف مع أصحابه.",
                    History = "اعتنى البدو في الجزيرة العربية بتربية الخيل الأصيلة قرونا طويلة، وكانت الفرس عزيزة عندهم كأنها من أهل البيت. وحملت الخيل فرسانها في الرحلات الطويلة وساحات الفروسية. واليوم تدخل دماء الخيل العربية في سلالات كثيرة، فتزيدها سرعة وقوة.",
                    Attack = "رفسة", Skill = "انطلاقة الصحراء", SkillDescription = "اندفاعة مدوية فوق الرمال.",
                },
            },
            new Animal
            {
                Id = "falcon",
                En = new Texts
                {
                    Name = "Falcon", The = "Falcon", Wild = "Wild Falcon", Style = "Swift glass cannon",
                    Description = "Falcons are fast birds of prey with sharp eyes and strong claws. A diving peregrine falcon can fly faster than 300 kilometres an hour. That makes it the fastest animal in the world.",
                    History = "Falconry, hunting with trained falcons, has been part of Arab life for many centuries. Bedouin hunters used falcons to catch hares and birds, bringing fresh food to the desert camp. Today falconry is a proud tradition in Saudi Arabia, and UNESCO lists it as living cultural heritage.",
                    Attack = "Talon Strike", Skill = "Hunting Dive", SkillDescription = "A lightning-fast dive from the sky.",
                },
                Ar = new Texts
                {
                    Name = "صقر", The = "الصقر", Wild = "الصقر البري", Style = "سريع وفتاك لكنه هش",
                    Description = "الصقور طيور جارحة سريعة، حادة البصر، قوية المخالب. وينقض الصقر الشاهين من الأعلى بسرعة تتجاوز 300 كيلومتر في الساعة. وهذا يجعله أسرع حيوان في العالم.",
                    History = "الصقارة، أي الصيد بالصقور المدربة، جزء من حياة العرب منذ قرون طويلة. وكان البدو يصطادون بها الأرانب والحبارى ليحصلوا على طعام طازج لمخيماتهم. واليوم تفخر السعودية بالصقارة، وقد أدرجتها اليونسكو ضمن التراث الثقافي الحي للإنسانية.",
                    Attack = "ضربة المخلب", Skill = "انقضاض الصقر", SkillDescription = "انقضاض خاطف من السماء.",
                },
            },
            new Animal
            {
                Id = "saluki",
                En = new Texts
                {
                    Name = "Saluki", The = "Saluki", Wild = "Wild Saluki", Style = "Speedy sprinter",
                    Description = "The Saluki is a slim, long-legged hunting dog with a silky coat. It is one of the oldest dog breeds and runs very fast over long distances. Its soft eyes make it look kind and calm.",
                    History = "Bedouin people kept salukis to help hunt gazelles and hares in the open desert, often together with falcons. The Saluki was so respected that it was called El Hor, the noble one. Many tribes did not sell their salukis, but gave them as gifts.",
                    Attack = "Bite", Skill = "Sprint Chase", SkillDescription = "A burst of speed and a quick snap.",
                },
                Ar = new Texts
                {
                    Name = "سلوقي", The = "السلوقي", Wild = "السلوقي البري", Style = "سريع الجري",
                    Description = "السلوقي كلب صيد نحيل طويل الساقين وفروه ناعم كالحرير. وهو من أقدم سلالات الكلاب، ويجري بسرعة كبيرة لمسافات طويلة. وعيناه الحنونتان تمنحانه مظهرا لطيفا وهادئا.",
                    History = "رافق السلوقي البدو في رحلات الصيد، فكان يطارد الغزلان والأرانب في الصحراء المفتوحة، وغالبا مع الصقور. وكانوا يلقبونه \"الحر\" لنبله وأصالته. وكثير من القبائل لا تبيع كلابها السلوقية، بل تهديها لمن تحب.",
                    Attack = "عضة", Skill = "مطاردة خاطفة", SkillDescription = "اندفاعة سريعة تعقبها عضة خاطفة.",
                },
            },
            new Animal
            {
                Id = "arabian_oryx",
                En = new Texts
                {
                    Name = "Arabian Oryx", The = "Arabian Oryx", Wild = "Wild Arabian Oryx", Style = "Sturdy horn fighter",
                    Description = "The Arabian oryx is a pale antelope with two long, straight horns. Its white coat reflects the hot sun, and it can live a long time without drinking. It walks on wide hooves that suit soft sand.",
                    History = "Desert people hunted the oryx for meat and hide, and it nearly disappeared. By 1972 it was gone from the wild. Breeding programmes in zoos saved it, and today oryx live again in reserves in Saudi Arabia and Oman.",
                    Attack = "Horn Jab", Skill = "Spear Charge", SkillDescription = "Lowers its long horns and charges.",
                },
                Ar = new Texts
                {
                    Name = "مها عربي", The = "المها العربي", Wild = "المها العربي البري", Style = "متين ويقاتل بقرنيه",
                    Description = "المها العربي من الظباء، لونه أبيض فاتح وله قرنان طويلان مستقيمان. يعكس لونه الأبيض حرارة الشمس، ويستطيع أن يعيش وقتا طويلا دون ماء. وحوافره العريضة تساعده على السير فوق الرمال اللينة.",
                    History = "كان الناس يصطادون المها لأجل لحمه وجلده، حتى اختفى من البرية عام 1972. ثم أنقذته برامج التكاثر في الأسر، فعاد اليوم إلى محميات السعودية وسلطنة عمان. ووصف الشعراء عيون الجميلات بعيون المها.",
                    Attack = "طعنة القرن", Skill = "هجمة الرمح", SkillDescription = "يخفض قرنيه الطويلين ويهجم.",
                },
            },
            new Animal
            {
                Id = "arabian_gazelle",
                En = new Texts
                {
                    Name = "Arabian Gazelle", The = "Arabian Gazelle", Wild = "Wild Arabian Gazelle", Style = "Graceful & quick",
                    Description = "Gazelles are small, graceful antelopes with slim legs and curved horns. They can run very fast and leap high to escape danger. They live in dry plains and rocky hills, and eat leaves and grass.",
                    History = "Bedouin hunters followed gazelles with salukis and falcons, and gazelle meat was a valued food. Arab poets also praised the gazelle's beauty, often comparing lovely eyes to a gazelle's. Today, protected reserves in Saudi Arabia help gazelles stay safe.",
                    Attack = "Quick Kick", Skill = "Leap Dash", SkillDescription = "A high spring followed by a fast strike.",
                },
                Ar = new Texts
                {
                    Name = "غزال عربي", The = "الغزال العربي", Wild = "الغزال العربي البري", Style = "رشيق وسريع",
                    Description = "الغزلان ظباء صغيرة رشيقة، ساقاها نحيلتان وقرناها منحنيان. تعدو بسرعة كبيرة وتقفز عاليا لتنجو من الخطر. وتعيش في السهول الجافة والتلال الصخرية، وتأكل الأوراق والأعشاب.",
                    History = "تتبع الصيادون من البدو الغزلان بالكلاب السلوقية والصقور، وكان لحمها طعاما مرغوبا. وتغنى الشعراء العرب بجمال الغزال ورشاقته. واليوم تحمي المحميات في السعودية الغزلان لتبقى آمنة.",
                    Attack = "ركلة سريعة", Skill = "وثبة خاطفة", SkillDescription = "قفزة عالية تعقبها ضربة سريعة.",
                },
            },
            new Animal
            {
                Id = "arabian_wolf",
                En = new Texts
                {
                    Name = "Arabian Wolf", The = "Arabian Wolf", Wild = "Wild Arabian Wolf", Style = "Well-rounded hunter",
                    Description = "The Arabian wolf is a small wolf that lives in the deserts and rocky hills of Arabia. It has a short, sandy coat and big ears. It hunts at night for hares, rodents and birds, alone or in small packs.",
                    History = "Shepherds in Arabia knew the wolf well and kept careful watch over their flocks at night. The wolf also appears in many old Arabic proverbs and stories. By hunting rodents and hares, it helps keep the desert in balance.",
                    Attack = "Bite", Skill = "Howling Fang", SkillDescription = "A howl-charged bite.",
                },
                Ar = new Texts
                {
                    Name = "ذئب عربي", The = "الذئب العربي", Wild = "الذئب العربي البري", Style = "صياد متوازن",
                    Description = "الذئب العربي ذئب صغير الحجم يعيش في صحارى الجزيرة العربية وجبالها الصخرية. فروه قصير بلون الرمل وأذناه كبيرتان. يصطاد ليلا الأرانب والقوارض والطيور، وحده أو في مجموعات صغيرة.",
                    History = "عرف الرعاة في الجزيرة العربية الذئب جيدا، فكانوا يسهرون الليل على حراسة قطعانهم. وورد الذئب في كثير من الأمثال والقصص العربية القديمة. وهو بصيده للقوارض والأرانب يساعد على حفظ توازن الصحراء.",
                    Attack = "عضة", Skill = "الناب العاوي", SkillDescription = "عضة يسبقها عواء مرعب.",
                },
            },
            new Animal
            {
                Id = "arabian_fox",
                En = new Texts
                {
                    Name = "Arabian Fox", The = "Arabian Fox", Wild = "Wild Arabian Fox", Style = "Sly & speedy",
                    Description = "The Arabian fox is a small desert fox with huge ears and pale, sandy fur. Its big ears give off heat to keep it cool, and furry paws protect it from hot sand. It hunts at night for insects, mice and fruit.",
                    History = "Bedouin travellers knew the fox as a quick, clever survivor of the desert. It appears in many Arabic folk stories as a smart trickster. Foxes also eat mice and insects, which helps protect camps and farms from pests.",
                    Attack = "Scratch", Skill = "Sand Dash", SkillDescription = "Kicks up sand, then strikes in a flash.",
                },
                Ar = new Texts
                {
                    Name = "ثعلب عربي", The = "الثعلب العربي", Wild = "الثعلب العربي البري", Style = "مراوغ وسريع",
                    Description = "الثعلب العربي ثعلب صحراوي صغير له أذنان كبيرتان وفرو فاتح بلون الرمل. تساعده أذناه الكبيرتان على التخلص من الحرارة، وتحمي الشعيرات الكثيفة قدميه من الرمل الساخن. يصطاد ليلا الحشرات والفئران، ويأكل الفاكهة أيضا.",
                    History = "عرف المسافرون من البدو الثعلب كائنا ذكيا وسريعا في الصحراء. وفي القصص الشعبية العربية يظهر الثعلب ماكرا ومخادعا، حتى قالوا في المثل: \"أروغ من ثعلب\". وهو يأكل الفئران والحشرات، فيحمي المخيمات والمزارع من الآفات.",
                    Attack = "خدش", Skill = "مناورة الرمل", SkillDescription = "يرفع الرمل في الهواء ثم يضرب في لمح البصر.",
                },
            },
        };
    }
}
