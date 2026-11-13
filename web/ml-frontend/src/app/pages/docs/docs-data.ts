import { Block } from '../crisp/crisp-data';

export interface DocPageData {
  slug: string;
  title: string;
  lead: string;
  blocks: Block[];
}

export const DOCS: DocPageData[] = [
  {
    slug: 'user-manual',
    title: 'User manual',
    lead: 'How to use the Night Shift playtest studio, by role. Everything operational needs an account; the documentation and the game are public.',
    blocks: [
      {
        heading: 'Roles',
        table: {
          head: ['Role', 'Can do', 'Cannot do'],
          rows: [
            ['Playtester (every new account)', 'Play with the account, see and edit their own playtests, send and edit their own reports while open', 'See other people’s data, review reports, manage users'],
            ['Designer', 'Everything a playtester can, plus: see all playtests and reports, run the model review, decide and resolve reports, ask the model, download the CSV report', 'Manage users or see the monitoring'],
            ['Administrator', 'Everything, plus users and roles, the monitoring page and the simulated logging test', 'Change their own role or disable their own account'],
          ],
        },
      },
      {
        heading: 'Create an account and sign in',
        bullets: [
          'Open Sign in → Create one. Choose a username (3–32 letters, numbers, dots, dashes or underscores) and a password of at least 8 characters with a letter and a number.',
          'New accounts are playtesters. Ask an administrator if you need the designer role.',
          'Five wrong passwords lock the account for five minutes. Change your password in My account (click your name in the studio).',
        ],
      },
      {
        heading: 'Playtester: record a playtest',
        bullets: [
          'Studio → Playtests → Play with my account. The game opens with your save; every night you finish (caught, escaped or out of time) is added to your playtests automatically, marked “game”.',
          'You can also Record a playtest by hand (night, outcome, time, objectives). Nights measured by the game can only get notes; nights entered by hand can be edited or deleted.',
        ],
      },
      {
        heading: 'Playtester: report what Pockets did',
        bullets: [
          'Studio → Behaviour reports → New report (or “Report” next to a playtest).',
          'Kind: “Pockets’ behaviour” for something the clown did, “Bug” for anything else broken, “Feedback” for suggestions.',
          'For behaviour, describe the situation as well as you remember it: the room he was in, the last noise and how loud, where he last saw you, how far into the night, whether his sense was charged; then choose what he did next.',
          'You can edit or delete the report until a designer reviews it. Afterwards you see the model’s verdict and the designer’s decision with their note.',
        ],
      },
      {
        heading: 'Designer: review and decide',
        bullets: [
          'Dashboard shows the queue. Open a behaviour report and press “Ask the trained network”.',
          'The review shows the probability the network gives to every action in that situation and to the move the playtester saw. At 15% or more it is “the brain would do this” (suggestion: expected behaviour); below it is “unlikely”, and a move the brain cannot choose there is “impossible” (suggestion: bug).',
          'Decide “Expected behaviour” (closes the report; explain it to the playtester) or “Bug” (sent to fix). Overriding the suggestion is allowed; say why in the note.',
          'When a bug is fixed, write what changed and in which build, and resolve it.',
          'Model & history lets you ask the network about any situation and lists every inference with its user, date, result and model version. The dashboard links a CSV with every playtest.',
        ],
      },
      {
        heading: 'Administrator: users and monitoring',
        bullets: [
          'Users: change roles and disable accounts. The system keeps at least one active administrator.',
          'Monitoring: errors, response times, inferences, input drift, real catch rate and designer–model agreement over the last 7 days, with alerts.',
          '“Run a simulated logging test” adds rows marked SIMULATED to test the monitoring; they only appear with the switch on and can be deleted at any time.',
        ],
      },
      {
        heading: 'Limits of the model’s verdict',
        bullets: [
          'The situation in a report is what the playtester remembers, not the game’s exact state.',
          'The network is frozen: it does not learn from reports. Reports feed the next training run.',
          'The 15% threshold is a design choice; the designer always has the last word.',
        ],
      },
    ],
  },
  {
    slug: 'technical-manual',
    title: 'Technical manual',
    lead: 'Architecture, database, API, security, deployment and maintenance of the Night Shift web information system.',
    blocks: [
      {
        heading: 'Architecture',
        table: {
          head: ['Layer', 'Technology', 'Responsibility'],
          rows: [
            ['Client', 'Angular 22 + Bootstrap 5 (web/ml-frontend)', 'PGC documentation, CRISP-ML pages, studio pages, the game page'],
            ['Game', 'Unity 6 WebGL build (web/game)', 'The 3D game; sends its save (with the play log) to /api/save'],
            ['API', 'Flask 3.1 on gunicorn (web/app.py, auth.py, studio.py)', 'Sessions, roles, business rules, model inference, logging'],
            ['Model', 'numpy forward pass of freddy_v19 (web/freddy_brain.py, data/FreddyPolicy.bytes)', 'The same 100 inputs and masked softmax as the game'],
            ['Database', 'PostgreSQL on Render (SQLite locally) through web/db.py', 'Users, playtests, reports, inferences, request and error logs, saves'],
          ],
        },
        code:
          'Browser ──HTTPS──> Flask (Render)\n   │  Angular pages        ├─ /api/auth/*    sign-in, sessions (HttpOnly cookie)\n   │  Unity WebGL game ──> ├─ /api/save/*    saves → playtests (import)\n   │                       ├─ /api/studio/*  playtests, reports, review, users, monitoring\n   │                       ├─ /api/brain/*   public demo inference\n   │                       └─ freddy_brain.py (numpy, freddy_v19 weights)\n   └───────────────────────── PostgreSQL',
      },
      {
        heading: 'Database model',
        table: {
          head: ['Table', 'Key fields', 'Relations'],
          rows: [
            ['users', 'username (unique), password_hash (scrypt), role, player_id (unique), active, failed_logins, locked_until', '—'],
            ['playtests', 'source (game / manual / simulated), fingerprint (unique), night, outcome, seconds, objectives, heard, seen, searches, threat', 'user_id → users'],
            ['tickets', 'kind, title, description, night, situation (JSON), observed_action, status, ai_verdict, decision, notes', 'reporter_id, designer_id → users; playtest_id → playtests'],
            ['inferences', 'model_version, situation, result, top_action, observed_probability, verdict, latency_ms, simulated', 'user_id → users; ticket_id → tickets'],
            ['request_log / error_log', 'time, method, path, status, latency / message', 'user_id → users'],
            ['saves / link_codes', 'player id, save JSON / short codes to move a save', 'player_id ↔ users.player_id'],
          ],
          note: 'Tables are created automatically on start (CREATE TABLE IF NOT EXISTS), so a new database needs no manual setup.',
        },
      },
      {
        heading: 'Business rules',
        bullets: [
          'Report states: open → reviewed (model) → expected | bug → resolved (bugs only). Behaviour reports need the model review before a decision; other kinds go straight to a decision.',
          'A reporter edits or deletes a report only while it is open; afterwards only an administrator can delete it.',
          'Nights imported from the game keep their measurements; only notes change. Objectives done ≤ objectives of the night; sense hits ≤ sense uses; nights 1–7.',
          'Model verdict: probability of the observed move ≥ 15% → expected; lower → unlikely; illegal from that room → impossible.',
        ],
      },
      {
        heading: 'API',
        table: {
          head: ['Method and route', 'Role', 'Purpose'],
          rows: [
            ['POST /api/auth/register, /login, /logout; GET /me; POST /password', 'public / signed in', 'Accounts and sessions'],
            ['GET, POST /api/studio/playtests; GET, PUT, DELETE /playtests/:id', 'playtester (own) / designer (all)', 'Playtest CRUD'],
            ['GET, POST /api/studio/tickets; GET, PUT, DELETE /tickets/:id', 'playtester (own) / designer (all)', 'Report CRUD'],
            ['POST /api/studio/tickets/:id/review, /decision, /resolve', 'designer', 'Model review and workflow'],
            ['POST /api/studio/predict; GET /inferences; GET /reports/playtests.csv', 'designer', 'Model on demand, history, report'],
            ['GET /api/studio/dashboard, /model', 'signed in', 'Indicators and model card'],
            ['GET, PUT /api/studio/users', 'admin', 'Users and roles'],
            ['GET /api/studio/monitoring; POST, DELETE /monitoring/simulate', 'admin', 'Monitoring and the simulated test'],
            ['POST /api/brain/decide; GET /api/model/info, /api/results, /api/runs, /api/plots/*', 'public', 'Demo and evidence'],
          ],
        },
      },
      {
        heading: 'Security',
        bullets: [
          'Passwords: Werkzeug scrypt hashes; at least 8 characters with a letter and a number; 5 failures lock the account for 5 minutes; failed logins take the same time whether or not the user exists.',
          'Sessions: signed Flask cookie, HttpOnly, SameSite=Lax, Secure on Render, 7 days.',
          'Authorization on the server for every studio route (the Angular guards only hide pages). Registration ignores any role sent by the client; nobody can change their own role.',
          'State-changing studio calls must be JSON (a cross-site HTML form cannot send it). Inputs are validated with ranges and lengths; SQL uses parameters only.',
          'Secrets (DATABASE_URL, SECRET_KEY, ADMIN_PASSWORD) live in the host’s environment, never in the repository.',
        ],
      },
      {
        heading: 'Deployment and configuration',
        code:
          '# render.yaml (Blueprint): rootDir web, build = npm build + pip install, start = gunicorn app:app\n# Environment variables (Render dashboard, never committed):\nDATABASE_URL=postgresql://...      # Render PostgreSQL\nSECRET_KEY=<random 64 hex>         # signs the session cookie\nADMIN_USERNAME=<first admin>       # created on start if missing\nADMIN_PASSWORD=<strong password>\n# Local: no DATABASE_URL -> SQLite files in web/data (git-ignored)',
        bullets: [
          'Every push to main on GitHub deploys to Render. The game build must stay under 100 MB per file (GitHub limit).',
          'Render’s free PostgreSQL expires 30 days after creation: export the data (CSV report, /api/save/export) or move DATABASE_URL to another Postgres before that.',
        ],
      },
      {
        heading: 'Testing',
        bullets: [
          'API end-to-end test with Flask’s test client (44 checks): registration cannot grant a role, lockout, every permission boundary, playtest CRUD and validation, save import and de-duplication, the full report workflow with the model review, monitoring with simulated data.',
          'Model: the server’s forward pass matches the ONNX model’s action in 13,060 of 13,060 states.',
          'Front end: production build, every page checked at 390 px width without horizontal overflow.',
        ],
      },
      {
        heading: 'Model versioning and updates',
        bullets: [
          'The model version string includes a hash of the weights file, and every inference row stores it.',
          'Updating the model: export the new weights to web/data/FreddyPolicy.bytes and the ONNX to the game, deploy; old inferences keep their old version. The full procedure is in CRISP-ML phase 6.',
        ],
        links: [{ label: 'Monitoring and maintenance', url: '/crisp-ml/monitoring' }],
      },
    ],
  },
  {
    slug: 'training-guide',
    title: 'Training guide',
    lead: 'A 10-minute walkthrough that shows a new user the whole workflow, from signing up to a decision made with the model.',
    blocks: [
      {
        heading: '1. Playtester (4 minutes)',
        bullets: [
          'Create an account (Sign in → Create one). You land on the dashboard.',
          'Click “Play the game with your account”, skip the intro with Enter and play night 1 until you escape or get caught.',
          'Back in the studio, open Playtests: the night is there, marked “game”. Add a note.',
          'Click “Report” on that night. Describe a moment: Pockets in the Promenade, a loud noise in the Ticket Lobby, he went to the Birthday Tent. Send it.',
        ],
      },
      {
        heading: '2. Designer (4 minutes)',
        bullets: [
          'Sign in as a designer. The dashboard shows one report in the queue.',
          'Open it and click “Ask the trained network”. Read the verdict: the network gives the reported move a high probability, so it suggests “expected behaviour”.',
          'Choose “Expected behaviour”, write a note for the playtester (“he heard the lever and walked toward it, as trained”) and save.',
          'Open Model & history: the inference is there with the user, date, result and model version.',
        ],
      },
      {
        heading: '3. Administrator (2 minutes)',
        bullets: [
          'Users: promote the new playtester to designer and back.',
          'Monitoring: run the simulated logging test, switch on “include simulated data”, read the drift alert it produces, then delete the simulated rows.',
        ],
      },
    ],
  },
  {
    slug: 'support',
    title: 'Support: incidents and feedback',
    lead: 'How problems are reported, who handles them and how users give feedback.',
    blocks: [
      {
        heading: 'Reporting an incident',
        bullets: [
          'Users: open Behaviour reports → New report and choose “Bug” (or “Pockets’ behaviour” for something the clown did). Say what you did, what you expected and what happened.',
          'Automatic: every server error is logged with the time, route and user, and appears on the monitoring page.',
          'Without an account: open an issue in the project repository.',
        ],
      },
      {
        heading: 'Severity and response',
        table: {
          head: ['Severity', 'Example', 'Target response'],
          rows: [
            ['Critical', 'The site or the game does not load; sign-in fails for everyone', 'Same day'],
            ['High', 'Data not saved; the model review fails', 'Within 2 days'],
            ['Normal', 'A wrong behaviour of Pockets; a display problem', 'Next weekly review'],
            ['Feedback', 'Suggestions and opinions', 'Answered with a decision note'],
          ],
        },
      },
      {
        heading: 'Feedback',
        bullets: [
          'Send a report of kind “Feedback”. A designer answers it with a decision note you can read in the report.',
          'Feedback and confirmed behaviour reports are reviewed before each training run; the training log records what changed because of them.',
        ],
      },
    ],
  },
];
