/**
 * The six CRISP-ML(Q) phases of Night Shift, written from the project's own evidence
 * (docs/ml-design.md, docs/training-log.md, web/data/*.json). Every number here can be traced
 * to one of those files or to a live endpoint of this site.
 */

export interface Block {
  heading: string;
  text?: string[];
  bullets?: string[];
  table?: { head: string[]; rows: string[][]; note?: string };
  image?: { src: string; alt: string; caption: string };
  code?: string;
  links?: { label: string; url: string }[];
  callout?: { tone: 'info' | 'warning' | 'success'; text: string };
}

export interface Phase {
  slug: string;
  number: number;
  title: string;
  question: string;
  summary: string;
  blocks: Block[];
}

export const PHASES: Phase[] = [
  {
    slug: 'business-data-understanding',
    number: 1,
    title: 'Business and Data Understanding',
    question: 'What problem do we solve, for whom, and what data can teach a model to solve it?',
    summary:
      'Horror-game enemies follow hand-written rules that players memorize. We framed the enemy as a reinforcement learning agent that must hunt the player using only what it perceives, defined measurable success criteria and checked that the data (simulated hunts) is sufficient and relevant.',
    blocks: [
      {
        heading: 'Problem and context',
        text: [
          'In most horror games the enemy runs a script: patrol, chase on sight, give up, repeat. After a few nights players learn the pattern and the fear disappears. The Machine Learning course asked for a non-player character built with Unity ML-Agents; we chose a first-person horror game, Night Shift, set in Bellwether’s Winter Circus, where the hunter, Pockets the clown, decides with a trained neural network instead of rules.',
          'The player is a night guard who must complete objectives (power cells, breakers, a keycard, a tape) and escape through tonight’s exit. Pockets has to find them by sound, sightings and a limited “sense” of fear, never by knowing where they are.',
        ],
      },
      {
        heading: 'Stakeholders and beneficiaries',
        table: {
          head: ['Stakeholder', 'Interest', 'How they benefit'],
          rows: [
            ['Players', 'A fair enemy that is hard to predict', 'Every night plays out differently; the enemy never cheats'],
            ['Development team (J. Nuncira, M. Moreno)', 'An enemy that does not need hand-tuned rules for every room', 'Behaviour comes from rewards; new rooms need retraining, not new scripts'],
            ['Playtesters and designers', 'Tell real bugs from learned decisions', 'The playtest studio checks reports against the network (Activity 2)'],
            ['Course and instructor', 'A complete, traceable ML project', 'Evidence for every phase on this site, the repository and the report'],
          ],
        },
      },
      {
        heading: 'From business objectives to ML objectives',
        table: {
          head: ['Business objective', 'ML objective', 'Measured by'],
          rows: [
            ['An enemy players cannot memorize', 'Learn a stochastic policy π(a | s) over 17 actions that maximizes the expected return', 'Catch rate against three survivor styles'],
            ['Fair: it never cheats', 'Observations hold only what Pockets can know; the player’s true room appears only in the reward shaping', 'Review of the 100 inputs (Data Engineering)'],
            ['Better than a hand-written enemy', 'Beat the scripted hunter with the same senses', 'Catch rate with 95% Wilson intervals, 400 nights per cell'],
            ['Runs live in a browser game', 'A small network (100 → 256 → 256 → 17) with fast inference', 'Inference time per decision'],
            ['Players can see it think', 'Expose the probability of every action', 'AI inspector and end-of-night replay in the game'],
          ],
        },
      },
      {
        heading: 'Questions the model answers and the learning task',
        bullets: [
          'Given what Pockets perceives right now, which of his 17 actions should he take? (move to a neighbouring room, stay and listen, open one of up to three hiding spots, or sense).',
          'How likely is each action? The probabilities drive the game, the AI inspector and the studio’s report reviews.',
          'Learning task: reinforcement learning on a partially observable Markov decision process with discrete, masked actions. There are no labels: the signal is a reward (+1 catch, −1 escape, −0.5 time up, plus small shaping terms).',
        ],
      },
      {
        heading: 'Data sources, variables and size',
        table: {
          head: ['Source', 'What it contains', 'Size'],
          rows: [
            ['Training simulation (Unity scene HuntTraining, 32 parallel hunts)', 'Experience tuples: 100 observations, the action, the reward, episode ends', '54.8 million decisions over 19 versions (≈ 15.5 h of training)'],
            ['Survivor bot (domain randomization)', 'A new play style every night: tracker 0.5–0.97, hide 0.4–0.8, run 0.05–0.45, patience 2–5, curiosity 0–0.3', 'One random personality per training night'],
            ['Evaluation runs', 'Catch, escape and time-up rates, searches, decisions per night for 3 policies × 3 survivors', '400 nights per cell (3,600 nights for v19)'],
            ['TensorBoard exports', 'Reward curves of every run', '19 CSV files in web/data/curves'],
            ['Playtests with people', 'Play logs of real nights (outcome, time, noises heard, searches, threat level)', 'Grows with the playtest studio'],
          ],
          note: 'Target variable: none in the supervised sense. The quantity optimized is the discounted return (γ = 0.99). The 100 input variables are listed on the Data Engineering page.',
        },
      },
      {
        heading: 'Data quality, relevance and sufficiency',
        bullets: [
          'Quality: simulated data has no missing or corrupted values by construction; every input is bounded to [0, 1].',
          'Relevance: the risk is the gap between the bot and people (a policy that beats a bot may feel wrong against humans). Mitigated with domain randomization, a curious bot that visits dead ends, and playtests that led to v10, v11–v12 and v17–v19.',
          'Sufficiency: data is generated on demand, so sufficiency is judged by the reward plateau and by behaviour probes. Rare situations (the staff wing when Pockets is far away) stay under-represented; they were found by probing the network, not by the catch rate.',
        ],
      },
      {
        heading: 'Feasibility, limitations and risks',
        table: {
          head: ['Risk', 'Impact', 'Mitigation'],
          rows: [
            ['Reward hacking (a bonus becomes the goal)', 'High reward, poor hunting (v13)', 'Select models by measured behaviour, not by reward'],
            ['Exploiting the map or the bot', 'Camping the only exit door (v5)', 'Random exits and sockets (v6, v12), curious bot (v9)'],
            ['Sim-to-human gap', 'Behaviour that feels unfair to people', 'Playtests, adaptive threat level, behaviour reports in the studio'],
            ['Frozen policy', 'No learning during a match', 'Stated openly; retraining procedure in Monitoring'],
            ['Compute and time', 'A 5-week course with two people', '5M steps in 80–90 min on an Apple M-series laptop; fine-tuning from previous runs'],
            ['Web size limits', 'GitHub rejects files over 100 MB', 'Web build profile: game.data.gz at 74 MB'],
          ],
        },
      },
      {
        heading: 'Success criteria',
        table: {
          head: ['Criterion', 'Target', 'Result'],
          rows: [
            ['Catch rate against the Expert survivor', '≥ 90%', '97.8% in evaluation; 98.8% (97.1–99.5) on the independent test ✔'],
            ['Versus the scripted hunter, same senses', 'Not worse on any survivor', 'Equal on Novice and Normal, +6.3 points on Expert ✔'],
            ['No camping loophole', 'Never stays twice beside a hidden guard', '0 of 275 cases in the harness ✔'],
            ['Inference time', '< 10 ms per decision', '≈ 0.07 ms on the server (numpy forward pass) ✔'],
            ['Web delivery', 'Every file under 100 MB', 'Largest file 74 MB ✔'],
            ['Visible ML', 'Probabilities shown in the game and the web', 'AI inspector, replay, Try the brain, studio reviews ✔'],
          ],
        },
        links: [{ label: 'Detailed MDP and concepts', url: '/ml/concepts' }],
      },
    ],
  },
  {
    slug: 'data-engineering',
    number: 2,
    title: 'Data Engineering',
    question: 'How is what Pockets knows turned into the 100 numbers the network reads, without leaking anything?',
    summary:
      'We selected the inputs from what the hunter could really know, encoded and scaled them to [0, 1] with fixed constants, handled “never happened” values with explicit encodings, engineered route features from the map, analysed the input space with PCA and separated training, validation and test streams.',
    blocks: [
      {
        heading: 'Feature selection and its evolution',
        text: ['The rule: an input may only contain what Pockets could know. The player’s real room is never an input; tonight’s exit and live sockets are not either.'],
        table: {
          head: ['Versions', 'Inputs', 'Why it changed'],
          rows: [
            ['v1–v2', '43', '3 × 3 grid of 9 rooms: room, noise memory, last sighting, visits, progress'],
            ['v3', '54', 'New action SENSE: readiness, where it found the player, time since then'],
            ['v4', '79', 'Bigger map for the 3D game (14 rooms on two floors)'],
            ['v5–v6', '64', 'One floor in two wings (11 rooms)'],
            ['v7–v8', '74', 'The final 13-room map with objectives'],
            ['v9–v19', '100', '+26 engineered route features (below)'],
          ],
        },
      },
      {
        heading: 'The 100 inputs',
        table: {
          head: ['Index', 'Feature', 'Encoding'],
          rows: [
            ['0–12', 'Pockets’ own room', 'one-hot'],
            ['13–25', 'Noise memory per room', 'heard level / 3, fades ×0.8 per decision'],
            ['26–38', 'Room where he last saw the player', 'one-hot, all zeros if never'],
            ['39', 'Time since that sighting', 'decisions / 60, clipped to 1'],
            ['40–52', 'Time since he checked each room', 'decisions / 60, clipped to 1'],
            ['53', 'Sees the player now', '0 / 1'],
            ['54', 'Share of tonight’s objectives done', 'done / total'],
            ['55', 'Night progress', 'decision / 240'],
            ['56–58', 'Spots in his room already searched', '0 / 1 each'],
            ['59', 'Sense readiness', '1 − cooldown / 30'],
            ['60–72', 'Room where sense last found the player', 'one-hot'],
            ['73', 'Time since that', 'decisions / 60, clipped to 1'],
            ['74–86', 'Neighbour leads toward the loudest noise', '0 / 1 (engineered, v9)'],
            ['87–99', 'Neighbour leads toward the last sighting or sense', '0 / 1 (engineered, v9)'],
          ],
        },
      },
      {
        heading: 'Missing values, duplicates, inconsistencies and outliers',
        bullets: [
          'Missing: “never seen” and “never sensed” are not missing values but states, encoded explicitly (all-zero one-hot and time = 1, the maximum).',
          'Outliers: every time feature saturates at 60 decisions (clipped to 1) and the noise memory at level 3, so no input leaves [0, 1].',
          'Inconsistencies: action masking removes impossible actions (rooms not connected, spots that do not exist, sense while recharging), so the network can never pick them; the web API validates every situation with the same ranges.',
          'Duplicates: experience is generated, not collected, so there are none to remove; in the playtest studio each imported night has a fingerprint so a save sent twice is stored once.',
        ],
      },
      {
        heading: 'Feature engineering: the map in Pockets’ head',
        text: [
          'With only one-hot rooms the network had to learn every pair of rooms from experience, and pairs the training rarely showed were never learned: v8 and v9, in the Arcade with a noise in the Dining Hall (today The Midway and The Big Ring), went toward it only 0–9% of the time. v9 added, for each neighbouring room, whether a step there brings him closer to the loudest noise and to the last sighting (breadth-first distances on the room graph). It is not secret information: Pockets knows the building.',
        ],
      },
      {
        heading: 'Before and after: one situation',
        text: ['Pockets is on the Promenade, a loud noise came from the Ticket Lobby (the exit lever), he saw the player in the Birthday Tent 35 s ago, every objective is done and 60% of the night has passed.'],
        code:
          '{"freddy_room": 7, "noises": {"10": 3}, "last_seen_room": 8, "seconds_since_seen": 35,\n "rooms_not_checked": [0, 1, 2, 4], "objectives": 1.0, "night_progress": 0.6, "sense_ready": true}',
        table: {
          head: ['Non-zero inputs after processing', 'Value', 'Meaning'],
          rows: [
            ['7', '1.0', 'He is on the Promenade (one-hot)'],
            ['23', '1.0', 'Noise memory, Ticket Lobby: level 3 / 3'],
            ['34', '1.0', 'Last seen in the Birthday Tent (one-hot)'],
            ['39', '0.167', '35 s ÷ 3.5 s per decision = 10 decisions ÷ 60'],
            ['40–52', '1.0 / 0.15', 'Rooms not checked for long = 1, the rest 0.15, his own room 0'],
            ['54', '1.0', 'All objectives done'],
            ['55', '0.6', 'Night progress'],
            ['59', '1.0', 'Sense ready'],
            ['73', '1.0', 'Never sensed'],
            ['82, 95', '1.0', 'Birthday Tent leads toward the noise and toward the sighting'],
          ],
          note: 'The other 78 inputs are 0. The network answers: go to the Birthday Tent 97.7%, sense 2.3%.',
        },
      },
      {
        heading: 'Dimensionality reduction (PCA) of the input space',
        image: { src: '/api/plots/pca', alt: 'PCA of 2,000 situations coloured by the network decision', caption: 'PCA over 2,000 seeded synthetic situations, computed live by the Flask API with numpy (SVD).' },
        bullets: [
          'The first two components explain only 7.1% and 5.1% of the variance, and 46 components are needed to reach 90%: the inputs are mostly independent one-hot blocks, with little redundancy to remove.',
          'The four kinds of decision (move, stay, search, sense) overlap in the PC1–PC2 plane: no low-dimensional linear projection separates them, which supports a non-linear model (the two Swish layers of 256 units).',
          'Decision: keep all 100 inputs. Each block has a meaning the AI inspector shows to the player, and a 100-input layer costs nothing at inference time. PCA is used as analysis, not as preprocessing.',
        ],
      },
      {
        heading: 'Partitions and leakage',
        table: {
          head: ['Partition', 'How it is produced', 'Used for'],
          rows: [
            ['Training', '32 parallel hunts, random seeds 42 + 7919·i, a random survivor personality every night', 'PPO updates'],
            ['Validation', 'Behaviour harness: 300 nights and targeted probes (go to a noise, stay twice, open a spot)', 'Choosing between versions and when to stop'],
            ['Evaluation', 'Evaluation runner, seeds 1000 + 7919·i, three fixed survivor presets (training only sees random personalities), 400 nights per cell', 'Comparing kept versions with the baselines'],
            ['Independent test', 'The same runner on seeds 500000 + 7919·i, never used before, run once after choosing v19', 'Final reported performance'],
          ],
          note: 'There is no fitted preprocessing: every scaling constant (÷3, ÷60, ÷240) is fixed by design and ML-Agents normalization is off (normalize: false). Nothing is estimated from any data split, so preprocessing cannot leak test information into training.',
        },
        links: [{ label: 'Observation and action tables in the model page', url: '/ml/model' }],
      },
    ],
  },
  {
    slug: 'model-engineering',
    number: 3,
    title: 'Model Engineering',
    question: 'Which algorithms fit the problem, how was the model trained and how did domain knowledge shape it?',
    summary:
      'We compared a trained PPO policy against two non-learning baselines with the same senses, justified PPO over other algorithms, documented the network and hyperparameters, and recorded 19 training runs whose changes came from measured behaviour and playtests.',
    blocks: [
      {
        heading: 'Algorithms compared',
        table: {
          head: ['Policy', 'How it decides', 'Role'],
          rows: [
            ['Random', 'A uniform random legal action each decision', 'Lower baseline'],
            ['Scripted hunter', 'Chases the last sighting, investigates the loudest noise and searches every spot there, otherwise patrols the room unvisited for longest', 'Strong hand-written baseline with the same information'],
            ['PPO network (trained)', 'Neural policy trained with Proximal Policy Optimization', 'The model'],
          ],
        },
      },
      {
        heading: 'Why PPO',
        bullets: [
          'No labelled examples of good hunting exist, so supervised learning or imitation would need data we do not have.',
          'The state is continuous and large (100 inputs), so tabular Q-learning is impractical.',
          'PPO is the default trainer of Unity ML-Agents, stable with on-policy updates and clipped steps (Schulman et al., 2017), and handles discrete actions with invalid-action masking well (Huang & Ontañón, 2022).',
          'SAC was not trained: with discrete masked actions and a 5-week schedule we kept one well-understood algorithm and spent the time on the environment and the reward, where the problems actually were.',
        ],
      },
      {
        heading: 'Network and hyperparameters (training/config/freddy_ppo.yaml)',
        table: {
          head: ['Setting', 'Value'],
          rows: [
            ['Network', '100 inputs → 256 → 256 (Swish) → 17 action logits, masked softmax'],
            ['Learning rate', '3.0e-4, linear schedule'],
            ['Batch / buffer', '1,024 / 20,480'],
            ['Epochs per update', '3'],
            ['Clip ε / entropy β / GAE λ', '0.2 / 5.0e-3 / 0.95'],
            ['Discount γ', '0.99'],
            ['Time horizon', '128'],
            ['Steps', '3M (v1–v3), 5M (v4–v8), then fine-tuning 1–2.5M from the previous run'],
          ],
          note: 'The ML-Agents defaults were kept; tuning went into the reward terms, the environment and the training length (early stopping chosen by behaviour, v14).',
        },
      },
      {
        heading: 'Experiment record (19 versions)',
        table: {
          head: ['Version', 'Change', 'Result', 'Decision'],
          rows: [
            ['v1–v3', 'First hunt on a 9-room grid; impatient bot; SENSE action', '92–100% catch', 'Move to the 3D map'],
            ['v5', 'Two wings, one exit', 'Camped the only exit door', 'Random exits (v6)'],
            ['v7–v8', '13-room map with objectives; +0.02 for walking into a noise room', '99% catch, but went to a noise next door only 35%', 'Engineer route inputs'],
            ['v9–v10', '+26 route inputs, curious bot; reward for every step toward a fresh noise', 'Goes to a loud noise 100%', 'Keep, keep probing'],
            ['v13', 'Patrol bonus doubled', 'Reward 1.9 but Expert catch 65%', 'Rejected: reward hacking'],
            ['v15–v16', 'Hunter rewards instead of patrol bonuses; bot hears instead of seeing', 'Hunts by clues', 'Fine-tune'],
            ['v17–v19', 'Closing camping loopholes found in playtests', 'Expert 97.8%, never stays twice beside a hidden guard', 'v19 in the game'],
          ],
        },
        image: { src: '/api/plots/versions', alt: 'Training reward versus evaluated catch rate across versions', caption: 'A higher reward is not a better hunter: v13 has the highest reward and the lowest catch rate.' },
        links: [
          { label: 'Training curves of every run', url: '/ml/training' },
          { label: 'Experiments and findings', url: '/ml/experiments' },
        ],
      },
      {
        heading: 'How domain knowledge informed the model',
        bullets: [
          'Horror design: the enemy must hunt by ear (the game tells the player so), hence the noise inputs and the reward for investigating fresh noises.',
          'Friday the 13th: The Game inspired SENSE (feel the player’s fear, with a cooldown) and doors; Alien: Isolation inspired the noise-making toys.',
          'Fairness: privileged information (the player’s real room) is used only in a potential-based shaping term (Ng et al., 1999), which speeds learning without changing the optimal policy, and never as an input.',
          'Playtests with people found the behaviours the catch rate hid (walking past a noise next door, camping a checked locker); each one became a measured probe and a reward change.',
        ],
      },
    ],
  },
  {
    slug: 'evaluation',
    number: 4,
    title: 'Model Evaluation',
    question: 'Does the trained network hunt better than the alternatives, and can we trust the numbers?',
    summary:
      'The primary metric is the catch rate with 95% Wilson intervals over 400 independent nights per survivor style, complemented by efficiency and behaviour metrics. v19 matches the scripted hunter against novice and normal survivors and beats it against experts while searching less.',
    blocks: [
      {
        heading: 'Metrics and why',
        table: {
          head: ['Metric', 'Why it matters'],
          rows: [
            ['Catch rate (primary) with 95% Wilson CI', 'The business goal is a hunter that catches; Wilson intervals are reliable for proportions near 100%'],
            ['Escape and time-up rates', 'How the nights the hunter loses end'],
            ['Decisions until the catch', 'Efficiency: a hunter that catches late is less threatening'],
            ['Searches per night and failed-search rate', 'Quality: opening every locker is a dull, unfair strategy'],
            ['Behaviour probes', 'A high catch rate can hide degenerate behaviour (camping, ignoring noises)'],
          ],
        },
      },
      {
        heading: 'Validation scheme',
        bullets: [
          'k-fold cross-validation assumes a fixed i.i.d. dataset; reinforcement learning episodes are generated by the policy itself, so the equivalent is repeated evaluation on independent episodes.',
          'Three survivor presets (Novice, Normal, Expert) act as folds of play style; training never uses them as such (it draws a random personality every night).',
          'Each cell plays 400 nights over 16 independent random streams, with a 95% Wilson interval on every rate.',
        ],
      },
      {
        heading: 'Results (freddy_v19, 400 nights per cell)',
        table: {
          head: ['Survivor', 'Trained (PPO)', 'Scripted', 'Random'],
          rows: [
            ['Novice', '100.0% (99.1–100.0)', '99.5% (98.2–99.9)', '88.8% (85.3–91.5)'],
            ['Normal', '98.5% (96.8–99.3)', '98.5% (96.8–99.3)', '77.2% (72.9–81.1)'],
            ['Expert', '97.8% (95.8–98.8)', '91.5% (88.4–93.8)', '73.0% (68.5–77.1)'],
          ],
          note: 'Searches per night: trained 1.5 / 3.6 / 4.1, scripted 2.3 / 3.7 / 4.6, random 13.1 / 17.7 / 19.7 (Novice / Normal / Expert).',
        },
        image: { src: '/api/plots/catch-rate', alt: 'Catch rate by survivor and policy with confidence intervals', caption: 'Catch rate with 95% Wilson intervals, drawn live from web/data/results.json.' },
      },
      {
        heading: 'Overfitting, underfitting, bias and risks',
        bullets: [
          'Overfitting to the reward: v13 raised its reward to 1.9 while its Expert catch rate fell to 65%; the reward curve alone would have chosen the worst model.',
          'Overfitting to the environment: v5 learned to camp the only exit, and v10 to wait on the only crossing between wings; random exits, random sockets and a second corridor removed those shortcuts.',
          'Underfitting: v7–v8 caught 99% but ignored noises in rarely visited room pairs; the engineered route inputs fixed it.',
          'Bias: trained against bots, the policy may behave differently against people. The threat level adapts the body (speed, pace) to each player without changing the network, and real nights are monitored in the studio.',
          'Learned preference: of the spots the network opens, 61–65% are lockers (scripted 57%), because the survivors it trained against hide there more often: a population-level adaptation, not online learning.',
        ],
      },
      {
        heading: 'Review with people who know the domain',
        text: [
          'Playtests with players of horror games found what the metrics hid: Pockets walking back and forth while the player made noise next door (led to v10), pacing on the only crossing (v11–v12), and standing beside a locker the guard was holding their breath in (v17–v19).',
        ],
        callout: {
          tone: 'warning',
          text: 'Expert review record: [name and role of the reviewer], [date], [their feedback on whether the hunter feels fair and threatening], [what we changed or decided because of it].',
        },
      },
      {
        heading: 'Model selection and final test on an independent set',
        bullets: [
          'Criterion: the highest catch rate against the Expert survivor among versions with no behaviour loophole in the harness; reward is never the criterion.',
          'Selected: freddy_v19 (fine-tuned from v18 for 1M steps).',
          'The results above (seeds 1000 + 7919·i) were looked at while choosing between versions, so they are not an untouched test. After the selection, v19 and both baselines were run once more on random streams no run had ever used (seeds 500000 + 7919·i), 400 nights per cell. Nothing was changed after this run.',
        ],
        table: {
          head: ['Survivor (independent test, seeds 500000+)', 'Trained (PPO)', 'Scripted', 'Random'],
          rows: [
            ['Novice', '100.0% (99.1–100.0)', '99.5% (98.2–99.9)', '90.5% (87.2–93.0)'],
            ['Normal', '98.2% (96.4–99.2)', '95.5% (93.0–97.1)', '79.0% (74.7–82.7)'],
            ['Expert', '98.8% (97.1–99.5)', '92.2% (89.2–94.5)', '74.8% (70.3–78.8)'],
          ],
          note: 'Final reported performance: 98.8% against the Expert survivor, 6.6 points above the hand-written hunter, with fewer searches per night (trained 1.9 / 3.0 / 3.4, scripted 2.5 / 4.4 / 4.8). Raw file: web/data/heldout.json (seed_base 500000), produced by HuntEvaluationRunner -seedbase 500000 -nights 400.',
        },
        links: [
          { label: 'All metrics and the SENSE comparison', url: '/ml/metrics' },
          { label: 'Ask the network yourself', url: '/ml/application' },
        ],
      },
    ],
  },
  {
    slug: 'deployment',
    number: 5,
    title: 'Model Deployment',
    question: 'How does the selected model reach the game, the website and the information system?',
    summary:
      'freddy_v19 runs inside the Unity game as an ONNX model and, with exactly the same preprocessing, on the Flask server as a numpy forward pass. The server exposes it to the public demo and to the playtest studio, where it reviews behaviour reports.',
    blocks: [
      {
        heading: 'Saved artifacts',
        table: {
          head: ['Artifact', 'Where', 'Used by'],
          rows: [
            ['freddy_v19.onnx', 'unity/Assets/ML/Models/', 'The game, through ML-Agents (Inference Engine)'],
            ['FreddyPolicy.bytes', 'web/data/', 'The Flask server (weights exported by training/scripts/export_policy_weights.py)'],
            ['Preprocessing', 'FreddyObservations.cs (game) and web/freddy_brain.py (server)', 'The same 100 inputs from the same situation'],
            ['Training configuration', 'training/config/*.yaml', 'Reproducing the runs'],
          ],
          note: 'The server’s forward pass was checked against the ONNX model: the same most likely action in 13,060 of 13,060 states.',
        },
      },
      {
        heading: 'Functional demonstration',
        bullets: [
          'Public: “Try the brain” describes a situation and shows the probability of every action.',
          'Integrated: in the playtest studio a designer runs the network on a reported situation and decides whether the report is a bug (Activity 2).',
          'In the game: the AI inspector (Tab) and the end-of-night replay show the same probabilities.',
        ],
        links: [
          { label: 'Try the brain', url: '/ml/application' },
          { label: 'Playtest studio', url: '/studio' },
          { label: 'Play the game', url: '/play' },
        ],
      },
      {
        heading: 'Inputs and outputs of the API',
        table: {
          head: ['Field', 'Type', 'Meaning'],
          rows: [
            ['freddy_room', '0–12', 'Room Pockets is in'],
            ['noises', '{room: 1–3}', 'Noises he remembers (footsteps, running, loud)'],
            ['last_seen_room, seconds_since_seen', '0–12, seconds', 'Last sighting'],
            ['rooms_not_checked', 'list of rooms', 'Rooms not checked for a long time'],
            ['sees_player, sense_ready', 'booleans', 'Sight now; sense charged'],
            ['objectives, night_progress', '0–1', 'Share of objectives done; time of night'],
            ['searched_here, sensed_room', 'spots, room', 'Spots already opened here; where sense found the player'],
          ],
          note: 'POST /api/brain/decide returns every legal action with its probability (highest first), the 100 inputs it built and the model version. POST /api/studio/predict and the report review add the comparison with an observed move.',
        },
      },
      {
        heading: 'Dependencies and how to run it',
        code:
          '# server (Python 3.11): Flask 3.1, numpy 2.2, matplotlib 3.10, psycopg 3.2, gunicorn\ncd web && pip install -r requirements.txt\ncd ml-frontend && npm install && npm run build && cd ..\nADMIN_USERNAME=admin ADMIN_PASSWORD=change-me1 flask --app app run\n# game: Unity 6000.0.84f1 with ML-Agents 4.1; menu Night Shift > Build WebGL',
        text: ['On Render the same build runs with gunicorn; DATABASE_URL points to Postgres and SECRET_KEY, ADMIN_USERNAME and ADMIN_PASSWORD are set as secrets, never in the repository.'],
      },
      {
        heading: 'Integration, users, incidents and feedback',
        bullets: [
          'Integration plan (Activity 2, done): the playtest studio stores playtests and behaviour reports, runs the model on the server and records every inference with the user, date, result and model version.',
          'User instructions: the user manual and the training guide in Documentation.',
          'Incidents: errors are logged automatically with the route and the user; players report problems as “bug” reports in the studio; administrators review the monitoring page.',
          'Feedback: “feedback” reports in the studio, answered by a designer with a decision note.',
        ],
        links: [
          { label: 'User manual', url: '/docs/user-manual' },
          { label: 'Support: incidents and feedback', url: '/docs/support' },
        ],
      },
    ],
  },
  {
    slug: 'monitoring',
    number: 6,
    title: 'Monitoring and Maintenance',
    question: 'How do we know the model keeps working, and what do we do when it does not?',
    summary:
      'The system logs every API request, error and inference. Monitoring compares recent inputs with earlier ones (population stability index), real outcomes with the evaluation and designers’ decisions with the model’s suggestions, and raises alerts that trigger review or retraining.',
    blocks: [
      {
        heading: 'Indicators and thresholds',
        table: {
          head: ['Indicator', 'Source', 'Alert when'],
          rows: [
            ['Server error rate', 'request_log', '> 5% over 7 days'],
            ['Response time (p95)', 'request_log', '> 1,500 ms'],
            ['Inference time', 'inferences', 'Tracked (≈ 0.1 ms); a jump means the server is overloaded'],
            ['Input drift (PSI) on Pockets’ room, noises, sightings, night progress', 'inferences', 'PSI > 0.25'],
            ['Real catch rate of the last 30 playtests', 'playtests', 'Outside 40–95% (with at least 10)'],
            ['Designer agreement with the model', 'tickets', '< 70% (with at least 10 decided)'],
          ],
        },
      },
      {
        heading: 'Detecting data drift and performance degradation',
        bullets: [
          'Data drift: the inference history is split into its older and newer halves and every monitored feature is compared with the population stability index (< 0.1 stable, 0.1–0.25 watch, > 0.25 alert). A new game rule or room changes what Pockets perceives and shows up here first.',
          'Performance: predictions are compared with real outcomes when they exist. Each behaviour report gets the model’s verdict and then a designer’s decision; the agreement rate measures how often the model’s judgement was right. Real playtests give the catch rate against people, compared with the offline evaluation.',
        ],
      },
      {
        heading: 'Review frequency, responsibilities and retraining criteria',
        table: {
          head: ['When', 'Who', 'What'],
          rows: [
            ['Weekly', 'Administrator', 'Review the monitoring page and the error log; open a bug report for any alert'],
            ['Every game build', 'Game owner', 'Run the walk audit and the behaviour harness; check the web build size'],
            ['Monthly or on alert', 'ML owner', 'Probe the network on reported situations; decide on retraining'],
          ],
          note: 'Retrain when: PSI > 0.25 for two consecutive weekly reviews; agreement < 70%; three or more confirmed bugs traced to brain decisions; or any change to the map, the inputs or the actions (mandatory).',
        },
      },
      {
        heading: 'Update procedure and quality controls',
        bullets: [
          'Train freddy_vN+1 (fine-tune from the current model), measure it in the harness, then run the evaluation runner against the baselines.',
          'Accept it only if it is not worse than the current model on any survivor and passes every behaviour probe.',
          'Export the ONNX model and the web weights, update the game, rebuild; the model version string (with a hash of the weights) changes in every new inference row, so results stay traceable.',
          'Rollback: previous models stay in unity/Assets/ML/Models and the repository history.',
        ],
      },
      {
        heading: 'Initial logging and monitoring test',
        text: [
          'The monitoring page (administrators) has a button that generates a SIMULATED operation: 60 inferences, 80 API requests and 20 playtests, all stored with simulated = 1 and shown only when the “include simulated data” switch is on. Half of the simulated inferences are shifted toward late-night situations in three rooms, so the drift check must fire.',
        ],
        callout: {
          tone: 'info',
          text: 'Result of the test (SIMULATED data): the PSI of Pockets’ room and of night progress rose far above 0.25 and the page raised drift alerts, while the real view stayed unaffected. Simulated rows are deleted with one click.',
        },
        links: [{ label: 'Monitoring (administrators)', url: '/studio/monitoring' }],
      },
    ],
  },
];
