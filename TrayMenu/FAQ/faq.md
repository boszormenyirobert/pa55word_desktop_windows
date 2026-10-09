# Desktop FAQ

## How does the desktop window work?

Starting the app always opens the appropriate window: registration (Bootstrap) when no profile is registered, Login when the active database has no active session, or Credentials when it does. The initial view uses the saved session state without waiting for network checks; background checks can invalidate expired or revoked access. You can move, resize, minimize, or maximize the registered desktop window using the standard Windows controls.

Closing the window with X hides it without stopping the background agent. Left-click or right-click the tray icon to open Credentials. If the active database has no active session, Login opens instead; an unregistered desktop opens registration. The tray icon does not show a menu. Launching the app again also brings the existing instance forward.

Minimizing the window keeps it on the taskbar. There is no longer an Exit command on the tray icon; closing the window keeps the background agent running.

The same startup behavior applies when the browser extension starts the agent. The legacy `--start-in-tray` argument no longer suppresses the window.

## How do I get started?

Install and register the mobile app first. The mobile app establishes your account and approves desktop access.

Open the desktop app, enter the server URL, create a profile, and scan the displayed QR code with the mobile app. Keep the mobile app online until desktop registration completes.

In Settings, Profile, Add new profile, the server URL and profile name are read-only while the generated QR code is displayed. Select Cancel to stop waiting for mobile confirmation and unlock the inputs. The form stays open with your entered values, so you can edit them and select Continue to generate a new QR code.

## How do I select the correct profile on a shared computer?

Use the active profile selector in the desktop app before signing in. The selected profile is also shown in the left menu under the ZeroIntrusion heading.

Only sign in to a server for the profile that owns the credentials you need. Signing in with one profile does not give access to another profile's credentials.

## How do I sign in to a server?

Select the server, choose Login, and approve the request in the mobile app. The desktop waits for the approval result and shows a tray notification for approval, rejection, timeout, or expired access.

If the server returns `no_devices`, the Login view explains that no mobile device registered to the account can authorize the request.

## How do I rename my profile?

Sign in with your profile to at least one server, then open Settings, Profile, and choose Rename profile. Enter a non-empty name and select Save name. This updates only your profile's local name in config.json, not server-side account data. Another profile's session cannot authorize the rename.

## How do I add a new database?
Use this option when no mobile device has yet been granted access to the database.

Open Manage Databases and choose Add a database. Enter the connection path and database name, then select Continue to generate a QR code.

Scan the QR code with a mobile device that can approve the connection. The mobile app grants this desktop access to the database.

## What is the difference between Add a database and granting access to an existing database?

Choose Add a database when no mobile device has yet been granted access to the database.

Choose Grant access to an existing database when the mobile device already has access and can approve this desktop connection. In both cases, scan the generated QR code with the mobile app.

## How do I add a second desktop?

Install the desktop app on the additional computer and use the same connection path. Complete its desktop registration by scanning the QR code with a mobile device that can approve the connection.

If the mobile device already has access to the database, use Grant access to an existing database from the new desktop after its initial setup.

## How do I create and organize credentials?

Sign in to the active server, then open Credentials. Create a group when you want to organize related entries, create the credentials you need, and assign them to the appropriate group.

The active server and selected group control the entries displayed in this view.

The full-height All credentials list on the left always shows every credential for the active server, regardless of the Group dropdown filter. Select a credential in the list to select its group and entry in the dropdowns and load its details into the input fields. Credentials without a group select Ungrouped. The list stays up to date after creating, editing, or deleting credentials.

When you close the panel and reopen it from the tray while Credentials is active, the group and entry selectors keep their current selections.

## How do I remove a device?

Open Settings, then Devices. Select the device you no longer trust and remove it. Removing a device prevents it from approving or accessing your account according to the server's device policy.

Keep at least one trusted device available before removing another device.

## How do I delete a desktop profile?

Open Settings, then Profile, and choose Delete profile. Sign in with that profile to every server registered in it before confirming deletion. Selecting a different active server does not change which registrations are deleted.

Deletion deregisters this desktop from each server; it does not delete accounts or credentials. If a server deletion fails, successfully deleted registrations are removed locally, while the remaining registrations and profile are kept for retry. The profile disappears after its final server registration is removed.

## Why cannot the desktop connect to a server?

When adding or syncing a server, the desktop first checks the server health endpoint. Check the server URL, network connection, and server availability if QR generation reports that the server is unavailable or unhealthy.

The mobile device must also be able to reach the server to approve registration or sign-in requests.

## What happens if I lose all of my devices?

Do not remove your final trusted device unless you have completed your organization's recovery process. Recovery options depend on the server and account policy; contact the server administrator or follow the recovery process provided by your organization.