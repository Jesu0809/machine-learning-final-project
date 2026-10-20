namespace NightShift.Game
{
    /// <summary>
    /// The story of the five nights: the intro narration, the phone call of each night and the lab
    /// notes lying around the building. The notes quote the real training of Freddy's brain
    /// (docs/training-log.md), so the story and the machine learning are the same thing.
    /// Voice clips: Assets/Audio/Story/intro_NN.wav and night{N}_NN.wav, one per line, in order.
    /// </summary>
    public static class StoryData
    {
        /// <summary>Title cards over the first shot of the intro (no voice).</summary>
        public static readonly string[] IntroCards =
        {
            "1987",
            "Freddy's Family Pizzeria closes its doors\nafter a series of... incidents.",
            "Thirty-eight years later."
        };

        /// <summary>
        /// The intro: a voicemail the new guard listens to in the car on the way to the pizzeria
        /// (the same voice as the phone calls). Line 0 is the answering machine.
        /// </summary>
        public static readonly string[] Intro =
        {
            "You have one new message.",
            "Hey, it's me, from the agency. Just checking you found the place. Freddy's. The old pizzeria on Route Nine.",
            "It closed back in eighty-seven, after the... incidents. Some robotics company bought it. Nightshift Labs.",
            "They threw everything out except the bear. And they gave him a new brain. A neural network, trained on thousands of fake nights.",
            "Now they want a real night. A real guard. That's you.",
            "Midnight to six. Easy money. And hey, if the bear moves... that's normal. Probably. Okay. Bye."
        };

        /// <summary>The phone call of each night (index 0 = night 1).</summary>
        public static readonly string[][] Calls =
        {
            new[]
            {
                "Hello? Hello, hello! Uh, welcome to your first night at the pizzeria. They asked me to walk you through it, since the last guy... well. Let's not get into that.",
                "So. The power is out, and three fuses are missing. Find them, put them in the exit panel, and the door opens. Some nights it is the front doors, some nights the loading door in the storage. Your list tells you which.",
                "Now, about Freddy. Management installed a new brain in him. A neural network. They trained it on thousands of nights against people like you. It does not follow a script. It learned.",
                "It cannot see through walls, but it listens. Running, picking up a fuse, working that exit panel. All loud. Walk when you can, and crouch when you really need to be quiet.",
                "If he gets close, hide. Lockers, cabinets, under the tables. And if he opens your locker, hold your breath with the space bar. Not for too long.",
                "Oh, and the air vents. There are grates all over the building, in the floor and in the walls. Crawl in and you come out in another room. He is too big for them, so in there he cannot reach you.",
                "Sprinting will not save you for long, you will run out of breath. And every fuse you take makes him angrier. Faster.",
                "One more thing. If you panic, he can sense it. The engineers added that. He decides on his own when to use it. So, stay calm.",
                "If you want to see what it is thinking, press TAB. They left their debug screen in. It shows what he heard, where he is going, and how sure the network is about every move. Use that.",
                "He is asleep on the stage right now. Look around while I am talking. When I hang up... he wakes up. Okay. Good luck. Talk to you tomorrow. Hopefully."
            },
            new[]
            {
                "Hey! You made it. Wow. Okay. Management is... thrilled. They want more data.",
                "So tonight is a little different. The fuses do not go into the exit panel anymore. They go into the breaker box in the storage room. Restore the breaker, then the exit gets power.",
                "I looked at last night's logs. Here is the thing: Freddy does not remember you. The network is frozen, it cannot learn during the night.",
                "But it was trained against thousands of guards. It knows what people do. People hide when they hear him. People run straight for the exit.",
                "So, do something people do not do. Throw one of those party horns. It cannot tell a horn from a person. To the network, a noise is a noise.",
                "And he is a little faster tonight. They tuned him. Okay. I will let you go."
            },
            new[]
            {
                "Night three. Okay. Security upgrade, apparently. The exit needs a keycard now. It is in the office drawer, but the drawer runs on the same power line. Breaker first, then the card.",
                "And some of the lights are dying. Nobody wants to pay for bulbs in a building that is officially closed.",
                "I found the old training notes. Early on, Freddy was useless. He walked in circles. He opened empty lockers all night. The reward was negative.",
                "It took about a million steps before he started checking the rooms next to where he heard you. Three million before he learned when to use his sense.",
                "The reward curve... it just goes up. It never goes back down. I do not like that. Anyway. Keep moving."
            },
            new[]
            {
                "Hey. Night four. I need to tell you something and I need you to keep working while I do.",
                "The guard before you. Management said he quit. I found his name today. It was in the training data.",
                "They did not just simulate guards. They recorded the real ones. Every night he worked here became more practice for Freddy.",
                "The lights are worse tonight, and your tracker is losing range. And Freddy senses fear more often now. They changed the settings after your last night.",
                "I think they are going to do the same with you. Every night you survive... teaches him something. Be careful what you teach him."
            },
            new[]
            {
                "This is the last night. Tomorrow they retrain Freddy with everything you did this week. Every hiding spot. Every horn. Every route.",
                "There is a tape in the office. The recordings. All of them. Take it with you. If anyone sees it, this place closes for good.",
                "Breaker, keycard, tape, then the exit. He will be fast tonight. Faster than you have seen. And he is awake already. I am sorry.",
                "If he catches you... well. At least he will have learned from the best. Go. Go now."
            }
        };

        /// <summary>Lab notes found around the building. Real numbers from the training of the brain.</summary>
        public static readonly string[] Notes =
        {
            "TRAINING LOG 0001\nReward per night: -0.46.\nSubject walks in circles and opens empty lockers. The survivor escapes almost every time.\nManagement unimpressed.",
            "TRAINING LOG 0214\nSubject has started standing in the dining hall.\nFrom there every room is two moves away and he hears more of the building.\nNobody taught him this.",
            "TRAINING LOG 1000\nOne million steps. Subject now searches the rooms next to where he heard a noise.\nSurvivors who hide as soon as he comes near are found in under a minute.",
            "MEMO - ENGINEERING\nThe network only sees what it hears and what it saw. It never knows where the guard really is.\nIt cannot tell a noise from the guard. Do NOT tell the guards about the party horns.",
            "TRAINING LOG 3000\nThree million steps. Catch rate against a hand-written hunter: 66%. Against our network: 99%.\nIt uses its sense about once per night and finds the guard 80% of the time. It waits until they are afraid.",
            "MEMO - SECURITY\nThe debug screen (TAB) shows the network's probabilities for every move.\nRemove it before the public launch. It shows exactly how he thinks.",
            "HANDWRITTEN NOTE\nIf you are reading this, crouch and turn your flashlight off. In the dark he only sees you up close.\nHe hears running. He hears fear. - R.",
            "PERSONNEL FILE (TORN)\nNight guard #4. Status: ...ed into training set.\nNights recorded: 5. Escapes: 0."
        };

        /// <summary>What changes each night, shown on the title card.</summary>
        public static readonly string[] NightChanges =
        {
            "",
            "Freddy is faster  ·  the breaker must be restored",
            "Freddy is faster  ·  lights are failing  ·  the exit needs a keycard",
            "Freddy senses more often  ·  the tracker loses range  ·  half the lights are dead",
            "Freddy is awake from the start  ·  find the tape  ·  get out"
        };
    }
}
