import { Component, inject, signal } from '@angular/core';
import { PercentPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ResultsService } from '../../services/results.service';
import { BrainService } from '../../services/brain.service';
import { Results } from '../../models/results.model';
import { NightRow } from '../../models/brain.model';

@Component({
  selector: 'app-home',
  imports: [RouterLink, PercentPipe],
  templateUrl: './home.html',
})
export class Home {
  private readonly resultsService = inject(ResultsService);
  private readonly brainService = inject(BrainService);
  protected readonly results = signal<Results | null>(null);
  protected readonly nightRows = signal<NightRow[]>([]);

  protected readonly survivors = ['Novice', 'Normal', 'Expert'];

  protected readonly policies = [
    { id: 'trained', name: 'Trained network', detail: 'PPO, learned from rewards' },
    { id: 'scripted', name: 'Hand-written hunter', detail: 'same senses, rules instead of learning' },
    { id: 'random', name: 'Random hunter', detail: 'the lower baseline' },
  ];

  protected readonly objectives = [
    {
      title: 'Prepare the data: model the hunt as a decision process and engineer the 100 inputs Pockets may know (never the player\'s position), with a simulation that generates training nights against varied survivors.',
      link: '/crisp-ml/data-engineering', linkText: 'Data engineering',
    },
    {
      title: 'Develop the model: train the policy with PPO, improving it version by version (v1 to v19) with documented reward and environment changes.',
      link: '/crisp-ml/model-engineering', linkText: 'Model engineering',
    },
    {
      title: 'Evaluate it against a hand-written hunter and a random one, 400 nights per survivor skill with 95% confidence intervals, and select the final model by measured behaviour.',
      link: '/crisp-ml/evaluation', linkText: 'Evaluation',
    },
    {
      title: 'Implement the game (desktop and browser) and a web information system with accounts and roles where the model reviews playtesters\' reports.',
      link: '/studio', linkText: 'Playtest studio',
    },
    {
      title: 'Validate the solution with playtests, inference history and monitoring of errors, response times, drift and real outcomes.',
      link: '/crisp-ml/monitoring', linkText: 'Monitoring',
    },
  ];

  protected readonly method = [
    { name: '1. Business and data understanding', when: 'CRISP-ML', link: '/crisp-ml/business-data-understanding', text: 'Problem, stakeholders, ML objectives, data sources and measurable success criteria.' },
    { name: '2. Data engineering', when: 'CRISP-ML', link: '/crisp-ml/data-engineering', text: 'The 100 inputs, encoding and scaling, engineered route features, PCA analysis, partitions without leakage.' },
    { name: '3. Model engineering', when: 'CRISP-ML', link: '/crisp-ml/model-engineering', text: 'PPO against two baselines, network and hyperparameters, 19 recorded training runs.' },
    { name: '4. Evaluation', when: 'CRISP-ML', link: '/crisp-ml/evaluation', text: 'Catch rates with Wilson intervals, overfitting and bias analysis, model selection.' },
    { name: '5. Deployment', when: 'CRISP-ML', link: '/crisp-ml/deployment', text: 'ONNX in the game, numpy on the server, the API and the demos.' },
    { name: '6. Monitoring and maintenance', when: 'CRISP-ML', link: '/crisp-ml/monitoring', text: 'Logs, drift (PSI), alerts, review frequency and retraining criteria.' },
    { name: 'Web information system', when: 'Activity 2', link: '/docs/technical-manual', text: 'Accounts and roles, playtests, behaviour reports reviewed by the model, indicators and monitoring.' },
  ];

  protected readonly schedule = [
    { activity: 'Project brief, repository and tools (scaffolding)', who: 'Both', planned: 'Before 10 Oct', actual: '15 Aug and 10 Oct', deliverable: 'Repository, docs/ml-design.md, setup guide' },
    { activity: 'Business and data understanding: problem, MDP, rewards', who: 'Both', planned: '10–16 Oct', actual: '10–12 Oct', deliverable: 'ml-design.md (MDP, rewards, success criteria)' },
    { activity: 'Simulation, survivor bot and training arena (data engineering)', who: 'J. Nuncira', planned: '10–23 Oct', actual: '10–12 Oct', deliverable: 'HuntState, SurvivorBot, 32-copy arena' },
    { activity: 'Training v1–v19 (model engineering)', who: 'J. Nuncira', planned: '24–30 Oct', actual: '11–29 Oct', deliverable: '19 runs, training-log.md, freddy_v19.onnx' },
    { activity: 'Game features: standalone game repository, 13-room map, doors and sprint', who: 'M. Moreno', planned: '17–23 Oct', actual: '17–27 Oct', deliverable: 'ml-game repository; the map the network trains on from v7; door and sprint mechanics' },
    { activity: 'Evaluation against the baselines', who: 'J. Nuncira', planned: '31 Oct–6 Nov', actual: '12–29 Oct (every kept version)', deliverable: 'results.json, Wilson intervals' },
    { activity: '3D game, seven nights, the circus and Pockets (deployment in the game)', who: 'Both', planned: '31 Oct–6 Nov', actual: '19 Oct–8 Nov', deliverable: 'Desktop and WebGL builds' },
    { activity: 'Website and Render deployment', who: 'J. Nuncira', planned: '7–13 Nov', actual: '20 Oct–8 Nov', deliverable: 'Public site with the game and the model' },
    { activity: 'Activity 1: CRISP-ML documentation in Flask', who: 'Both', planned: 'Due 14 Nov', actual: '13 Nov', deliverable: 'The six phase pages, PCA and version analysis' },
    { activity: 'Activity 2: playtest studio (accounts, roles, model in the workflow, monitoring)', who: 'Both', planned: 'Due 15 Nov', actual: '13 Nov', deliverable: 'Studio, manuals, test accounts' },
    { activity: 'Activity 3: final PGC documentation, report and video', who: 'Both', planned: 'Due 16 Nov', actual: 'In progress', deliverable: 'This page, technical report, 20-minute video' },
  ];

  protected readonly framework = [
    {
      title: 'Background: how game enemies are usually built',
      items: [
        { name: 'Finite-state machines and behaviour trees', text: 'Enemies switch between hand-written states or walk a tree of conditions and actions (Millington, 2019; Isla, 2005).', relation: 'The scripted hunter we compare against is this kind of enemy.' },
        { name: 'Goal-oriented planning', text: 'F.E.A.R. let enemies plan sequences of actions toward goals, still from hand-written rules and costs (Orkin, 2006).', relation: 'Shows the limit: better rules, but still rules a designer must foresee.' },
        { name: 'AI and games as a field', text: 'Learned agents are a growing alternative for game-playing and non-player characters (Yannakakis & Togelius, 2018).', relation: 'Places Pockets’ learned brain within current game AI research.' },
      ],
    },
    {
      title: 'Related projects and studies',
      items: [
        { name: 'Hide-and-seek', text: 'Agents trained with reinforcement learning in a hide-and-seek game invented strategies nobody programmed (Baker et al., 2020).', relation: 'Our hunt is a hide-and-seek game; Pockets also found unprogrammed strategies (guarding the only exit, the middle of the map).' },
        { name: 'Deep RL in video games', text: 'DQN learned Atari from pixels (Mnih et al., 2015); OpenAI Five and AlphaStar reached top human level in Dota 2 and StarCraft II (Berner et al., 2019; Vinyals et al., 2019).', relation: 'Same family of methods at a scale a laptop can train in 15 hours.' },
        { name: 'Unity ML-Agents', text: 'A toolkit that trains agents inside the Unity engine and exports them to run in the game (Juliani et al., 2018).', relation: 'The framework the course requires and the one Pockets was trained with.' },
      ],
    },
    {
      title: 'Theoretical foundations',
      items: [
        { name: 'Markov decision processes and reinforcement learning', text: 'An agent observes a state, acts and receives rewards, learning a policy that maximizes the expected discounted return (Sutton & Barto, 2018).', relation: 'The hunt is a partially observable MDP: 100 observations, 17 actions, rewards for catching.' },
        { name: 'Proximal Policy Optimization', text: 'A policy-gradient method that limits each update with a clipped objective, using generalized advantage estimation (Schulman et al., 2016, 2017).', relation: 'The algorithm that trained Pockets (clip 0.2, λ 0.95, γ 0.99).' },
        { name: 'Reward shaping', text: 'A potential-based bonus speeds learning without changing the optimal policy (Ng et al., 1999).', relation: 'The distance bonus uses the player’s true room only in the reward, never as an input.' },
        { name: 'Action masking and domain randomization', text: 'Masking invalid actions helps policy gradients (Huang & Ontañón, 2022); randomizing the training environment prevents overfitting to one setting (Tobin et al., 2017).', relation: 'Impossible moves are masked; the training survivor gets a new personality every night.' },
      ],
    },
    {
      title: 'Technical concepts',
      items: [
        { name: 'Neural network and ONNX', text: 'Two dense layers of 256 units with the Swish activation (Ramachandran et al., 2017), exported in the open ONNX format.', relation: 'freddy_v19.onnx runs in the game; the same weights run on the Flask server.' },
        { name: 'Evaluation statistics', text: 'The Wilson score interval gives reliable confidence intervals for proportions (Wilson, 1927).', relation: 'Every catch rate is reported with its 95% Wilson interval.' },
        { name: 'PCA and drift', text: 'Principal component analysis (Pearson, 1901) summarizes high-dimensional inputs; the population stability index (Siddiqi, 2006) measures distribution shift.', relation: 'PCA analyses the 100 inputs; PSI monitors the inputs the model receives in operation.' },
        { name: 'CRISP-ML(Q)', text: 'A process model for machine learning projects with quality assurance in six phases (Studer et al., 2021).', relation: 'The structure of the methodology and of the CRISP-ML pages.' },
      ],
    },
  ];

  protected readonly references = [
    'Baker, B., Kanitscheider, I., Markov, T., Wu, Y., Powell, G., McGrew, B., & Mordatch, I. (2020). Emergent tool use from multi-agent autocurricula. International Conference on Learning Representations.',
    'Berner, C., Brockman, G., Chan, B., Cheung, V., Dębiak, P., Dennison, C., Farhi, D., Fischer, Q., Hashme, S., Hesse, C., et al. (2019). Dota 2 with large scale deep reinforcement learning. arXiv:1912.06680.',
    'Huang, S., & Ontañón, S. (2022). A closer look at invalid action masking in policy gradient algorithms. The International FLAIRS Conference Proceedings, 35.',
    'Isla, D. (2005). Handling complexity in the Halo 2 AI. Game Developers Conference.',
    'Juliani, A., Berges, V.-P., Teng, E., Cohen, A., Harper, J., Elion, C., Goy, C., Gao, Y., Henry, H., Mattar, M., & Lange, D. (2018). Unity: A general platform for intelligent agents. arXiv:1809.02627.',
    'Millington, I. (2019). AI for games (3rd ed.). CRC Press.',
    'Mnih, V., Kavukcuoglu, K., Silver, D., Rusu, A. A., Veness, J., Bellemare, M. G., et al. (2015). Human-level control through deep reinforcement learning. Nature, 518(7540), 529–533.',
    'Ng, A. Y., Harada, D., & Russell, S. (1999). Policy invariance under reward transformations: Theory and application to reward shaping. Proceedings of the Sixteenth International Conference on Machine Learning, 278–287.',
    'Orkin, J. (2006). Three states and a plan: The A.I. of F.E.A.R. Game Developers Conference.',
    'Pearson, K. (1901). On lines and planes of closest fit to systems of points in space. The London, Edinburgh, and Dublin Philosophical Magazine and Journal of Science, 2(11), 559–572.',
    'Ramachandran, P., Zoph, B., & Le, Q. V. (2017). Searching for activation functions. arXiv:1710.05941.',
    'Schulman, J., Moritz, P., Levine, S., Jordan, M., & Abbeel, P. (2016). High-dimensional continuous control using generalized advantage estimation. International Conference on Learning Representations.',
    'Schulman, J., Wolski, F., Dhariwal, P., Radford, A., & Klimov, O. (2017). Proximal policy optimization algorithms. arXiv:1707.06347.',
    'Siddiqi, N. (2006). Credit risk scorecards: Developing and implementing intelligent credit scoring. Wiley.',
    'Studer, S., Bui, T. B., Drescher, C., Hanuschkin, A., Winkler, L., Peters, S., & Müller, K.-R. (2021). Towards CRISP-ML(Q): A machine learning process model with quality assurance methodology. Machine Learning and Knowledge Extraction, 3(2), 392–413.',
    'Sutton, R. S., & Barto, A. G. (2018). Reinforcement learning: An introduction (2nd ed.). MIT Press.',
    'Tobin, J., Fong, R., Ray, A., Schneider, J., Zaremba, W., & Abbeel, P. (2017). Domain randomization for transferring deep neural networks from simulation to the real world. 2017 IEEE/RSJ International Conference on Intelligent Robots and Systems, 23–30.',
    'Vinyals, O., Babuschkin, I., Czarnecki, W. M., Mathieu, M., Dudzik, A., Chung, J., et al. (2019). Grandmaster level in StarCraft II using multi-agent reinforcement learning. Nature, 575(7782), 350–354.',
    'Wilson, E. B. (1927). Probable inference, the law of succession, and statistical inference. Journal of the American Statistical Association, 22(158), 209–212.',
    'Yannakakis, G. N., & Togelius, J. (2018). Artificial intelligence and games. Springer.',
  ];

  protected readonly resources = [
    { label: 'Technical report (PDF)', text: 'The consolidated report of the three activities.', url: '' },
    { label: 'CRISP-ML evidence', text: 'The six phases with decisions, figures and results.', url: '/crisp-ml' },
    { label: 'Source code repository', text: 'Game, training, evaluation, website and README.', url: 'https://github.com/Jesu0809/machine-learning-final-project' },
    { label: 'Intelligent system', text: 'The playtest studio: accounts, roles, playtests, reports reviewed by the model.', url: '/studio' },
    { label: 'User documentation', text: 'User manual, technical manual, training guide and support.', url: '/docs/user-manual' },
    { label: 'Presentation video', text: 'The 20-minute presentation in English.', url: '' },
  ];

  protected readonly products = [
    { name: 'Software: the game', text: 'Night Shift for the desktop (Unity 6) and in the browser (WebGL), with seven nights, an encore and a show of the day.' },
    { name: 'Prototype: the trained model', text: 'freddy_v19.onnx, the PPO policy the game runs, with its training configuration and curves.' },
    { name: 'Web development', text: 'This site (Angular and Flask on Render): play, read how the brain works, and query the real network.' },
    { name: 'Technical document', text: 'The design of the decision problem, the training log of every version and the evaluation, in the repository\'s docs folder.' },
    { name: 'Digital content', text: 'The game\'s story (an intro film and the clown\'s speeches), told on the Story page.' },
    { name: 'Source code', text: 'The whole project, including the tools that sculpt and voice Pockets, in a public repository.' },
  ];

  protected readonly shots = [
    { file: 'intro-sign.jpg', caption: 'The intro: the guard drives up to the closed circus in the rain' },
    { file: 'intro-arrive.jpg', caption: 'The front doors, after a voicemail tells the story of his new brain' },
    { file: 'hall.jpg', caption: 'The ticket lobby, just past the chained doors' },
    { file: 'pockets.jpg', caption: 'Pockets wakes up in the Big Ring when a night begins' },
    { file: 'call.jpg', caption: 'Night 1: locked in the watch office while he explains the rules over the speakers' },
    { file: 'inspector.jpg', caption: 'AI inspector (Tab): what the network considered at its last decision' },
    { file: 'replay.jpg', caption: 'End of the night: how his brain hunted you, decision by decision' },
  ];

  constructor() {
    this.resultsService.getResults().subscribe({ next: (r) => this.results.set(r) });
    this.brainService.getModelInfo().subscribe({ next: (m) => this.nightRows.set(m.nights) });
  }

  protected rate(policy: string, survivor: string): number {
    const run = this.results()?.runs.find((r) => r.policy === policy && r.survivor === survivor);
    return run ? run.catch_rate : 0;
  }

  /** In-page links: scroll without changing the route (the fixed navbar is taken into account by CSS). */
  protected jump(event: Event, id: string): void {
    event.preventDefault();
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
