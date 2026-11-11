# Design prototype (approved 2026-11-11)

The approved screen designs for the Angular frontend. They are a design
reference, not code: the frontend is built from `../../frontend-design.md`, and
the API (`../../../../SmartTaskManagement-Backend/docs/openapi.json`) is the
authority for fields and rules.

- Live canvas (view, comment, Play): https://claude.ai/artifact/BXE1WVVCVv3FtKtjWxAnBJ
- These files are a copy of the canvas's `project/` folder. Each `*.dc.html` file
  needs the canvas runtime (`support.js`), so it does not render correctly when
  opened directly in a browser; use the canvas link to view it.

| File | Screen |
| --- | --- |
| `Main.dc.html` | v1 interactive prototype: visual baseline only, superseded by v2 |
| `D01-Login.dc.html` | Sign in |
| `D02-Dashboard.dc.html` | Dashboard, live (Project Manager), with a toast |
| `D03-Notifications.dc.html` | Notification panel |
| `D04-Projects.dc.html` | Projects list (Admin) |
| `D05-ProjectDetail.dc.html` | Project detail and its tasks (Admin) |
| `D06-Tasks.dc.html` | Tasks across all visible projects (Project Manager) |
| `D07-TaskDetail.dc.html` | Task detail (owning Project Manager) |
| `D08-Dialogs.dc.html` | New task, new project, change owner, delete project |
| `D09-Users.dc.html` | Users and roles (Admin) |
| `D10-States.dc.html` | Loading, empty, live connection, errors, session ended, 404 |
| `D11-EditDialogs.dc.html` | Edit project, edit task, assign task, delete task |
| `M01-Mobile.dc.html` | Mobile 360 px (Team Member): register, dashboard, menu, tasks, task detail, notifications |
| `canvas.json` | Canvas index: artboard layout and the decision notes |

What v2 changed from v1, and why, is in `canvas.json` (notes `demo` and
`v2decisions`) and in `../../frontend-design.md`.
