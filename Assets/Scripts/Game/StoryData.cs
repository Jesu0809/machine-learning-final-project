namespace NightShift.Game
{
    /// <summary>
    /// The story of the seven nights: the intro voicemail, what Pockets the clown says over the
    /// speakers each night, his taunts, and the lab notes lying around the building. The notes quote
    /// the real training of his brain (docs/training-log.md), so the story and the machine learning
    /// are the same thing. Voice clips: Assets/Audio/Story/intro_NN.wav, night{N}_NN.wav and
    /// taunt_{key}_NN.wav, made by tools/generate_story_audio.py from this file.
    /// </summary>
    public static class StoryData
    {
        /// <summary>Title cards over the first shot of the intro (no voice).</summary>
        public static readonly string[] IntroCards =
        {
            "1987",
            "Bellwether's Winter Circus closes its doors\nafter one last show.",
            "Thirty-eight years later."
        };

        /// <summary>
        /// The intro: a voicemail the new guard listens to in the car on the way to the circus.
        /// Line 0 is the answering machine.
        /// </summary>
        public static readonly string[] Intro =
        {
            "You have one new message.",
            "Hey, it's me, from the agency. Just checking you found the place. Bellwether's. The old winter circus on Route Nine.",
            "It closed back in eighty-seven, after the last show. Some robotics company bought it. Nightshift Labs.",
            "They threw out everything except the clown. And they gave him a new brain. A neural network, trained on thousands of fake nights.",
            "Now they want a real night. A real guard. That's you.",
            "Midnight to six. Easy money. And hey, if the clown moves... that's normal. Probably. Okay. Bye."
        };

        /// <summary>
        /// What Pockets says over the speakers each night (index 0 = night 1). He is not well. The
        /// voice tool reads the marks: CAPITALS are shouted, (parentheses) whispered, and "hee hee" /
        /// "ha ha" are giggled; every phrase gets its own pitch and speed.
        /// </summary>
        public static readonly string[][] Calls =
        {
            new[]
            {
                "Hello. Hello hello hello. HELLO! Ladies and gentlemen and boys and girls and... and you! You, in the little office! Hee hee hee hee! Welcome to the show! I am Pockets! I am the clown, I am the clown, I am the CLOWN! (Shhh. Do not clap. I hear everything.)",
                "The rules! They make me say the rules. Ha ha! Listen, listen. The lights are dead. Three sockets still work, three, never the same three, your list knows which. Find the power cells. Carry them one, at, a, time. Plug one into each live socket. Then the lever at the exit! Some nights the front doors, some nights the loading door in the trunk store. Your list knows. Your list knows everything. (I like your list.)",
                "They opened my head, you know. Hee hee! They put a new brain in. A neural network! It practiced on thousands of nights, thousands, thousands, against people just like you. Nobody wrote me a script. I learned my act all by myself. AREN'T YOU PROUD?",
                "I cannot see through walls. Boo hoo! But I listen. (I listen.) Running, grabbing a cell, plugging it in, that big loud lever. All LOUD. Walk when you can. Crouch when you must. Ha ha ha ha!",
                "And you! You listen too! There is no tracker here. My feet are heavy, my motors whir, and when I stand very, very still... I creak. Wear your headphones, it is only polite. Muffled means a wall between us. Dull and far means... (I am behind you.) Hee hee hee.",
                "When I come close, hide! Lockers, cabinets, under the tables, oh I love this game. And when I open your little door, you hold your breath with the space bar. Not too long! Nobody likes a blue face. MINE IS WHITE! Ha ha ha ha ha!",
                "The vents go room to room, in the walls, in the floor. I do not fit. I DO NOT FIT! I tried. I tried so many times. (How rude.)",
                "Sprinting will not save you, your little lungs give up. And every socket you power makes me faster. Faster, faster, FASTER! I do love an audience!",
                "One more thing. One more, one more. When you panic, I can feel it. Here. (Right here.) They added that. And I decide when to use it. Hee hee hee hee.",
                "Press TAB to see my thoughts! They left the panel open! What good is a magic trick if nobody sees the rabbit scream? Ha ha!",
                "I am asleep in the ring right now. Sleeping. Look around while I talk. Because when I stop talking... (when I stop talking...) I WAKE UP! Places, everybody! Places! Ha ha ha ha ha ha!"
            },
            new[]
            {
                "You came back. You came BACK! Nobody comes back! Hee hee hee! Oh, they are thrilled. They want more data. (So do I.)",
                "Colors tonight! Pretty colors! Yellow, blue and green. Each cell fits only the live socket of its color. Your list says which room. Do not mix them, do not mix them, I hate it when they mix.",
                "I left you toys on the floor! Little rubber ones! Step on one and it goes squeak, and I come RUNNING! Crouch and it stays quiet. Comedy is all about timing. Ha ha ha!",
                "My network is frozen, you know. It cannot learn while we play. But I keep everything in my pockets. (How you run. Where you hide. What you smell like.) Hee hee.",
                "Those party horns around the building? Throw one! I cannot tell a horn from a person. A noise is a noise is a noise is a noise. Go on, fool me! The children always did. (Always.)",
                "And I am faster tonight. They tuned me. They tuned me like a piano. Curtain!"
            },
            new[]
            {
                "Night three! Three three three! The breakers in the trunk store keep tripping. Flip them up by hand, one by one, then push the main handle. Every click is loud. THANK YOU! THANK YOU! You are too kind!",
                "Some of the lights are dying. Nobody pays for bulbs in a building that is officially closed. (I ate some of them.) Hee hee hee.",
                "And gifts! I left you gifts! Boxes with a little crank. Walk past one and up comes the surprise, and everybody hears it. Crouch past them. Or do not. I LOVE SURPRISES!",
                "Have you met the dummies? My old partners. They look at you when you are not looking. (That part is just for fun.) Mostly. Ha ha ha.",
                "Do you know what I was like at first? I walked in circles. Circles, circles, circles! I opened empty lockers all night and they gave me a negative reward! Negative! A million steps to check the room next to a noise. Three million to learn when to feel your fear.",
                "The reward curve only goes up. It never comes back down. Up and up and up. Ha ha ha ha ha! Why are you not laughing? KEEP MOVING!"
            },
            new[]
            {
                "Night four! Security wants the keycard swiped. It sits on a pedestal in the winter garden. Swipe it through the reader in the watch office. Slow and steady. That reader has no sense of humor. (I tried.)",
                "Let me tell you a story while you work. The guard before you. Management said he quit. Hee hee.",
                "He did not quit! HE DID NOT QUIT! He is in my act now! Every night he worked here went into my training. Every corner he hid in. (Every time he held his breath.)",
                "The rain is loud tonight, and the lights are worse. Listen hard. I feel fear more often now. They changed my settings. They changed my settings after your last night, and it tickles. Ha ha ha!",
                "Every night you survive teaches me something. (Be careful what you teach me.)"
            },
            new[]
            {
                "Night five! A change to the program! The exit motor is tired. Pull the lever and the door takes its time. Most of a minute, with the siren going, and every eye on you, every eye! Hide if you must. But be there when it opens. Hee hee hee!",
                "Do you know why they closed this place? August of eighty-seven. A boy called Tommy. His eighth birthday. Right here. (Somebody filmed it.)",
                "I was on the stage. I turned my head and looked at the camera. That is all. That is all! I was not built to turn my head. I had no brain back then, no brain at all. So who turned it? WHO TURNED IT? Ha ha ha ha!",
                "The tape stops there. They locked the doors and left me in the ring for thirty-eight years. Thirty-eight years. That is a long pause. (Even for me.)",
                "Places."
            },
            new[]
            {
                "Night six! No more lights! The breakers are fine, I simply prefer it this way. Use your flashlight if you are brave. I do like a spotlight on my stars. Hee hee hee hee!",
                "Hey, it's me, from the agency. Just checking you found the place.",
                "Good, is it not? IS IT NOT GOOD? Ha ha ha! There is no agency! There never was! They needed guards, so I made the calls. I learned that voice from guard number two. (He does not need it anymore.)",
                "Everybody I ever hunted is still in here. Everybody. I can do all of them. One day I will do you. (I already have your footsteps.)",
                "Tomorrow is the last show. Get some rest. In a locker, perhaps! HA HA HA HA HA!"
            },
            new[]
            {
                "This is it. This is it this is it this is it! The last show! After tonight they retrain me with all of it. Every hiding spot, every horn, every route. Hee hee hee hee!",
                "There is a tape. All the recordings, every guard, and that birthday. It is locked in a box on the ticket booth counter. Press the numbers one to nine, in order. Take it out with you and this place closes for good. (Please do not.)",
                "I bolted the vents! Nothing personal! Cells, breakers, keycard, tape, then the lever. And I am awake. I AM AWAKE ALREADY!",
                "If I catch you, at least I will have learned from the best. Ladies and gentlemen... boys and girls... (the finale.) HA HA HA HA HA HA!"
            }
        };

        /// <summary>The line of night 6 said in the voice of the agency's voicemail (index into its night's lines).</summary>
        public const int AgencyVoiceLine = 1;

        /// <summary>
        /// Short lines over the speakers during the night, by what just happened. One is picked at
        /// random; clips are taunt_{key}_NN.wav, in the same order.
        /// </summary>
        public static readonly (string key, string[] lines)[] Taunts =
        {
            ("seen", new[] { "THERE you are! Hee hee!", "Peekaboo. (Peekaboo.) PEEKABOO!", "I SEE YOU! Keep running, it is so good for the show!" }),
            ("lost", new[] { "Where did you go? Come back, come back, I was getting to the funny part!", "Hide and seek! My favorite! (I never lose twice.)" }),
            ("cell", new[] { "One more socket! I felt that in my shoes! Hee hee!", "Faster, you are making me faster! Thank you! THANK YOU!", "The lights come up. The audience LOVES you. Ha ha ha!" }),
            ("horn", new[] { "A horn! I know it is a trick! I am going anyway! Hee hee hee!", "Very funny. VERY FUNNY! That one is mine, by the way." }),
            ("hiding", new[] { "Still in there? (It must be getting warm.)", "I can wait. I waited thirty-eight years. Hee hee hee.", "That is a very small place to spend your last act. Ha ha!" }),
            ("sprint", new[] { "NO RUNNING UNDER THE BIG TOP! Ha ha ha!", "Run, run, run! I love the sound of your shoes!" }),
            ("toy", new[] { "Squeak squeak! Hee hee hee!", "I heard that. EVERYBODY heard that!" }),
            ("surprise", new[] { "SURPRISE! Ha ha ha ha!", "Ta-da! Now where are you standing? (I know where you are standing.)" }),
            ("exit", new[] { "Leaving before the bow? HOW RUDE!", "Hear that siren? That is your cue! Hee hee hee!" }),
            ("laugh", new[] { "Ha ha ha ha ha ha ha ha!", "Hee hee hee hee hee hee!", "(Hee hee.) Ha ha. HA HA HA HA!" }),
            ("habit_locker", new[] { "You do love a locker. I keep a list. (A long list.)", "Tonight I start with the lockers! Just so you know! Hee hee!" }),
            ("habit_cabinet", new[] { "The cabinets again? I keep a list! Ha ha!", "You and your cabinets. I will look there first tonight. (First.)" }),
            ("habit_table", new[] { "Under the tables, like the children! I keep a list!", "Tonight I look under the tables first. Hee hee hee." }),
            ("habit_fast", new[] { "You were in such a hurry last time! No time for the show? I CAN FIX THAT!", "You got out so quickly. They made me sharper, just for you. Hee hee." }),
            ("habit_slow", new[] { "Three curtains in a row! You are becoming part of the act! Ha ha ha!", "Do not worry. I went easy on you tonight. (A little.)" }),
            ("caught_0", new[] { "Last time I found you in the watch office. (I left the door open for you.)" }),
            ("caught_1", new[] { "Last time I found you in the prop workshop! Your favorite props are still there! Hee hee!" }),
            ("caught_2", new[] { "Last time I found you in the dressing rooms. (I kept your costume.)" }),
            ("caught_3", new[] { "Last time I found you in the backstage hall. It is a LONG way to run! Ha ha!" }),
            ("caught_4", new[] { "Last time I found you in the trunk store. (There is room in the trunks.)" }),
            ("caught_5", new[] { "Last time I found you in concessions! POPCORN? Hee hee hee!" }),
            ("caught_6", new[] { "Last time I found you in the ring. RIGHT IN MY SPOTLIGHT! Ha ha ha!" }),
            ("caught_7", new[] { "Last time I found you on the promenade. Nice walk? Hee hee." }),
            ("caught_8", new[] { "Last time I found you in the birthday tent. (I saved you a slice.)" }),
            ("caught_9", new[] { "Last time I found you on the midway. EVERYBODY WINS A PRIZE!" }),
            ("caught_10", new[] { "Last time I found you in the ticket lobby. So close to the door! Ha ha ha!" }),
            ("caught_11", new[] { "Last time I found you in the ringside lounge. Best seat in the house! Hee hee!" }),
            ("caught_12", new[] { "Last time I found you in the winter garden. (Nothing grows there anymore.)" })
        };

        /// <summary>Lab notes found around the building, two per night. Real numbers from the training of the brain.</summary>
        public static readonly string[] Notes =
        {
            "TRAINING LOG 0001\nReward per night: -0.46.\nSubject walks in circles and opens empty lockers. The survivor escapes almost every time.\nManagement unimpressed.",
            "PROGRAM - THE LAST SHOW\nPockets the Clown, a mechanical marvel!\nHe does not speak. He does not turn his head.\nHe is only a machine. Do not be afraid, children.",
            "TRAINING LOG 0214\nSubject has started standing in the big ring.\nFrom there every room is two moves away and he hears more of the building.\nNobody taught him this.",
            "MEMO - WARDROBE\nWhoever keeps filling the clown's pockets: stop.\nToday it was a child's shoe, a ticket stub and a guard's name tag.\nThey are sewn shut. How is it getting in there?",
            "TRAINING LOG 1000\nOne million steps. Subject now searches the rooms next to where he heard a noise.\nSurvivors who hide as soon as he comes near are found in under a minute.",
            "MEMO - ENGINEERING\nThe network only sees what it hears and what it saw. It never knows where the guard really is.\nIt cannot tell a noise from the guard. Do NOT tell the guards about the party horns.",
            "TRAINING LOG 3000\nThree million steps. Catch rate against a hand-written hunter: 66%. Against our network: 99%.\nIt uses its sense about once per night and finds the guard 80% of the time. It waits until they are afraid.",
            "MEMO - SECURITY\nThe debug screen (TAB) shows the network's probabilities for every move.\nRemove it before the public launch. It shows exactly how he thinks.",
            "HANDWRITTEN NOTE\nIf you are reading this, crouch and turn your flashlight off. In the dark he only sees you up close.\nHe hears running. He hears fear. - R.",
            "PERSONNEL FILE (TORN)\nNight guard #4. Status: ...ed into training set.\nNights recorded: 7. Escapes: 0.",
            "MEMO - AUDIO\nWe never gave the unit a voice. We removed the show tapes in 2024.\nWho is on the speakers at night? Security says the amplifiers are unplugged.",
            "HANDWRITTEN NOTE\nI called the agency to quit. The number is not in service.\nIt has not been in service since 1987. Then who hired me? - R.",
            "TRAINING LOG 19000\nversion 19. Catch rate 97.8% against the expert survivor. He no longer camps a locker he has checked.\nHe follows every sighting to the end.\nWe do not know how to make him stop improving.",
            "CHILD'S BIRTHDAY CARD\nTo Tommy, 8 today!\nHave fun at the circus and say hi to the clown.\nHe always looks at you, doesn't he? Love, Mom."
        };

        /// <summary>What changes each night, shown on the title card.</summary>
        public static readonly string[] NightChanges =
        {
            "",
            "He is faster  ·  each cell fits only the socket of its color  ·  rubber toys on the floor",
            "He is faster  ·  lights are failing  ·  reset the breakers by hand  ·  surprise boxes  ·  the dummies watch",
            "He senses more often  ·  the rain is louder: listen hard  ·  swipe the keycard",
            "He is faster  ·  the exit opens slowly, with a siren",
            "Almost no lights  ·  no more calls from the agency",
            "He is awake from the start  ·  the vents are bolted  ·  open the tape box  ·  get out"
        };

        public const string EncoreChanges = "Everything at once  ·  one mistake ends the run";

        public const string DailyChanges = "The same show for everyone today  ·  how fast can you get out?";
    }
}
