# Notebook: Hoop Land

## Game

- Name: Hoop Land (developer Koality Game)
- Store and install folder: Steam app 2453660, C:\Program Files (x86)\steam\steamapps\common\Hoop Land (find-game.ps1, 2026-11-15)
- Main executable: Hoop Land.exe
- Build proven on: game 1.12.1, Steam build 26283544 (find-game.ps1, 2026-11-15). GameAssembly.dll dated 2026-11-03. Last MelonLoader run 2026-09-13 said Game Version 1.09.76 (older build).
- 32 or 64 bit: 64 (MelonLoader log 2026-09-13: Game Arch x64)
- Engine family: unity-il2cpp (MelonLoader log: Game Type Il2cpp)
- Engine and version: Unity 2022.3.62f3 (MelonLoader log 2026-09-13)
- Anti-cheat or DRM: CodeStage Anti-Cheat Toolkit (Il2CppACTk.Runtime.dll), protects values only, offline game. Steam.

## Stages

- Recon (game-recon): done 2026-11-15 (mostly from the old mod's files + logs)
- Injection (inject): done 2026-11-15. MelonLoader v0.7.3 regenerated assemblies for game 1.12.1; our HoopLandAccess.dll loads (log line 'Hoop Land Access 0.1.0 loaded').
- Speech (speech): done 2026-11-15 via Tolk.dll (log: 'Speech ready. Screen reader: JAWS'). Decision: Tolk, not Prism, because it is proven with JAWS here.
- Screen text (screen-text): done v0.3.0 (key icons, rebind prompt, ratings, pickers). Earlier v0.2.2: Straight-list nav (explicit Selectable.navigation), option groups (chosen = white text vs blue 0.11,0.52,0.80), Cycle Left/Right pickers, row labels, Scrolling Header titles, numbered slots sorted, keyboard menu control. Claude-tested 11/15 on title, main menu, all Settings tabs, Exhibition, Season save slots, career hub. User retest pending.
- Scanner (scanner): replaced by court awareness keys (see HOOPLAND_PLAN.txt)
- Autowalk (autowalk): not needed in the usual form; basketball movement is the player's own

## Resources

- Old mod by user + Cursor AI (reference only, do not copy wholesale): C:\Users\micha\Downloads\New Hoopland basketball\HooplandAccessibilityMod
- Old Il2CppDumper dump (May 2026 build, names now stale): ...\New Hoopland basketball\HooplandGameSource\dump.cs
- Class extracts in research\ (from old dump)

## Season screens (0.9.0, 2026-11-16, UNTESTED)
- Season.cs: draft (static SeasonDraft.draftSelections/draftOrder/currentRound/currentPickNumber/simulatingPicks/isPaused; instance status,pg..c,picks,record,rosterSize), DraftSelection (commissionerText, round, pick, playerCard, continueButton), DraftLottery (top4, continueButton, static isSimulating), DeclareForDraft (question), PlayoffBracket (eastBrackets/westBrackets, currentRoundText). Proven only by compiling against 1.12.1 interop.
- HYPOTHESIS: draftOrder[picksMade] = team on the clock; bracket children = rounds/matchups; commissionerText holds the full announcement. First open of each screen auto-dumps UserData\HoopLandAccess_<screen>.txt to check.

## Decisions

- 2026-11-15: CLEAN REBUILD, user's choice. Old mod kept only as an idea list.
- 2026-11-15: Speech first, few sounds (user is confused by many tones). Sounds only for shot timing and split-second events.
- 2026-11-15: Use ONLY readable (non-obfuscated) game names: public fields/methods like PlayerManager.homePlayers, PlayerController.inPosition, PlayByPlay.GetPlayText. Never hard-code scrambled names like PLNAAIIJAFD.
- Controller-first, keyboard backup. No Ctrl/Insert/CapsLock bindings (JAWS).

## Proven facts

- Game code is OBFUSCATED and the scrambled names CHANGE every update (PlayByPlay private methods differ between May dump and 9/13 interop assemblies). Readable names (GetPlayText, ShareGIF, OnEnable) stayed. Proven 2026-11-15 by comparing dump.cs with ilspycmd output.
- Readable useful members (May dump + 9/13 assemblies): PlayerManager.homePlayers/roadPlayers/careerPlayer/static currentPassTarget/GetInbounder; PlayerController.inPosition, IsOpen(), CanDunk(), currentZone, possessionStatus, playerData, isPlayer, shotBegan, DisplayShotFeedback(string), basketball; ShotBar.greenBarWidth; PlayByPlay.GetPlayText(PlayByPlayData); PlayCallingMenu.GetAvailablePlays/TogglePlaysMenu; DefensiveMatchups.GetDefender. RE-CHECK on 11/03 build.

## Pitfalls

- An object named 'Game UI' also exists inside Settings (DISPLAY GAME UI row). Detect a live match by rootCanvas.name == 'Game UI', not by ancestor name.
- Claude test driving: keybd_event works for our GetAsyncKeyState keys; arrows need KEYEVENTF_EXTENDEDKEY; focus game with Alt tap + SetForegroundWindow. tools\drive.ps1 does all this.
- Killing the game mid-tutorial makes Start Game resume the tutorial next launch.

- keybd_event VK keys do NOT reach the game's menus (Enter/Down ignored in smoke test) though our own F2 worked. For Claude-driven tests, try SendInput scan codes or let the user press.

- Old mod: 21k lines, one 12.7k-line file, 74 FindObjectsOfType calls, never verified on court.

## Proven facts (2)

- The game HAS keyboard gameplay controls (pause screen list): WASD move, arrows = actions, Shift, Right Shift, Space = timeout. No keyboard MENU control (mod adds it, off during matches). Full list in HOOPLAND_RECAP.txt section 8.
- Menus use EventSystem + UnityEngine.UI Button; settings options are sibling Buttons under '<Setting>/Buttons' with a 'Header' Text.

## Proven facts (3) 2026-11-15
- Key pictures: InputIconManager (ScriptableObject) fields keyboardIcons/xboxIcons/... hold one Sprite per key; map sprite instance -> field name (Icons.cs). 172 named.
- Rebind prompt text "Press any button for <ACTION>" while waiting (RebindActionUI.m_RebindOverlay was NOT the active indicator).
- Ratings: Harmony postfix DisplayAttributes.UpdateAttributes(PlayerData); PlayerData.attributes.LAY etc = int[2] {current, max}. Bars on screen are always full width (useless).
- Match HUD texts: Game UI/Safe Area Y/Scoreboard/Elements/{Clock,Shot Clock,Period,Home|Road/{Name,Score}}/Text, Scoreboard/Commentator/Text. Score text animates "+3" first.
- Basketball statics readable: shooter, lastBallHandler, passTarget, looseBall, shotBegan, shotReleased, lastShotData(dis ft, pts). No static play-by-play list.
- Shot meter: ball.shotPower climbs 0 -> ~1.0 at green, then falls; green window ~40 ms. ShotBar green w=2.56 of yellow w=20.
- PlayerController.DisplayShotFeedback(string) Harmony prefix gives "TOO EARLY", "GOOD RELEASE".
- World units: 1 unit ~ 4.4 ft. Position x = along court, y = depth (W = +y), z = height. Hoops at Basketball.hoop[0/1] (one at x 9.05).
- Game keyboard input from Claude's tests needs SCAN CODES (keybd_event flag 8, +1 for arrows); VK codes do not reach the game, but do reach our GetAsyncKeyState.

## Proven facts (4) 2026-11-15 morning
- Game bindings (InputActionAsset PlayerInputActions): see UserData\HoopLandAccess_bindings.txt. Free in play: LT, RT, L3, R3.
- Create Player: rows <Row>/Buttons/{Decrease,Increase} Button + <Row>/Header + <Row>/Input Field/Text; names are InputField (typing works with VK keybd_event -> WM_CHAR). Appearance: Cycle Left/Color Button/Cycle Right, Hair Style middle "Button" opens a list.
- Player Upgrades: Upgrades/<Category>/Group/<Attr>/Buttons/Upgrade Button (text = cost), category points at <Category>/Group/XP/Points/Value.
- Il2Cpp wrappers are NOT reference-equal: List.Contains fails; compare by GetInstanceID (UiScreen.IsItem).
- TeamManager.direction = 0 in an exhibition match (not usable). Using PlayerController.InFrontCourt() instead (0.6.2, unverified).
- Exhibition Player Control screen: Player 1 row Road/CPU/Home option group, Position Lock buttons per team.
- Season modes: Career (one player), Campus (beta), Franchise (team), Commissioner.
- Created Ace Hooper career in SAVE 5 (Claude, 11/15).
- HYPOTHESIS: assistance rows (GameSettings component) are on the in-match Pause > SETTINGS screen in career games.


## Proven facts (6) 2026-11-15 evening, v0.8.0 (franchise SAVE 12, career SAVE 11 test)
- Il2Cpp PITFALL: RectTransform.GetWorldCorners(Vector3[]) returns zeros (managed array not copied back). Use rect + TransformPoint.
- Tables: rows = >=3 selectable siblings; header = nearest text line above with >=2 distinct texts aligned by word x (Text.preferredWidth + alignment). Section titles inside the rows container may carry a Selectable (Set Lineup "Starters/Bench").
- StarRating: mask Image width / "Stars Outline" width * 5 = stars (12.2/17.5 = 3.5).
- Skill bars (Create Player, Player Profile scouting): label Text with child "Fill" whose localScale.y = value/100; "Max" = potential. Create Player category Header is a child of the middle skill.
- Assistance rows: Settings/Panel/Panels/Control Panel/Mobile (inactive on PC). Mod activates Mobile and hides its non-assist rows. Rows: DEFENSIVE ASSISTANCE (default On), OFF-BALL ASSISTANCE (Off), DEFENSIVE AUTO-SWITCH (Off). Whether the PC game honors them in play: HYPOTHESIS (DefensiveAssistance() returned True on PC).
- Set Lineup: Enter picks, Enter on second row swaps (game is silent).
- Franchise flow: hub (Front Office, Roster, CONTINUE) -> offseason confirm -> Scoreboard day list -> Team Matchup (PLAY/COACH/SPECTATE, Offense/Defense gameplans, START GAME).
- Test helpers: tools\opensave12.ps1, tools\opencareer11.ps1, tools\startfranchise.ps1, tools\explore.ps1.

## Proven facts (5) 2026-11-15 afternoon, v0.7.0 (Claude test matches, BOS vs ATL exhibition, P1 keyboard)
- Court edges: PlayerController.StayInbounds(far point) gives x -9.67..9.67, y -4.75..4.75. Matches real step-outs (IsInbounds flips at y 5.03-5.35, x -10.5). Hoops x +-9.05.
- Your man: PlayerController.defensiveMatchup (enum R,PG,SG,SF,PF,C; type name scrambled -> read by reflection as int) == opponent playerType. Steady. GetDefender() flickers (contest-based) - do not use as primary.
- PlayerManager.currentPassTarget follows the stick; settle 0.4 s before speaking.
- Icon pass: passIcon sprite assigned only while Right Shift held; keyboard sprites 069-072 named by Icons.cs as arrows; PlayerManager.buttonIcons = button_east/south/west/north.
- Five-second violations: CPU teammate inbounder (GetInbounder, !IsInbounds) waits for human Call for Ball (Down/A). Proven: pressing Down -> inbound.
- GetInbounder() can return the dribbler in back court: require !IsInbounds() for inbounding.
- Fixed 3-ft line zone was crossed in 0.1-0.3 s when sprinting; speed-aware (0.8 s lead) gives ~0.8 s warning.
- Test helper tools\startmatch.ps1 starts a P1-vs-CPU exhibition from launch. tools\keepawake.ps1 keeps laptop awake.

## Playbot findings (Claude played ALA vs ALB exhibition with tools\playbot.ps1, speech-only)
- Inbound after opponent score: no cue -> 5-second violations. Fix 0.6.2: "You inbound the ball. Press A".
- Score speech leaked "+3" (int.TryParse accepts +3). Fixed.
- "You are not on the court" between plays (no isPlayer for a moment). Fixed: remember last player.
- Out of bounds by own team: needs boundary warnings (to do).

## Open questions
- TeamManager.direction sign = which hoop is attacked in a match (HYPOTHESIS: +1 = right). Check 'Rim:' log lines in a user-controlled match.
- DONE 0.6.0: controller in match = RT status, LT ball, R3 rim, L3 last line (game bindings dump: LT/RT/L3/R3 unused in play; UserData\HoopLandAccess_bindings.txt).
- Count-in shot mode: handle moves linearly; prediction error ~20 ms (corrected). Assists: GameSettings.offensiveAssistance/defensiveAssistance rows + SavePrefs bools; screen location not found yet.
- Make/miss in Shootaround (no commentator there).

- User says (11/15): Offensive/Defensive Assistance are NOT available in every mode; they unlock only with a created/solo (career) player or after some requirement. Find out exactly which modes. Plan must not rely on them everywhere.

- Does PlayByPlayData get created live during a game (usable as spoken commentary)?
- What strings does DisplayShotFeedback show (Early/Late/Perfect?)

## Test log

- 2026-11-15 19:50-20:20 Claude v0.8.0: franchise screens (team cards, tables, stars, staff, trade, lineup swap, gameplan), create player skills F3, assistance rows shown; match regression OK.

- 2026-11-15 13:22-13:51 Claude 7 matches v0.7.0: defense intro/matchup steady, F4 guard-spot, F3 + icon keys, teammate-inbound cue fixes 5-sec violations, half court + 8-sec prompt, sideline 0.8 s early. Steal reach (1.1 units) unverified.

- 2026-11-15 user test v0.2.2: controls screens missing key names, rebind silent, ratings without numbers, pickers 'Cycle Left'. Fixed in 0.3.0.
- 2026-11-15 Claude tests 0.4/0.5: CPU-vs-CPU exhibition speech OK; Shootaround: tone path runs, feedback TOO EARLY/GOOD RELEASE spoken, F8 rim distance OK.
- 2026-11-15 user test v0.1.0: most reads fine; settings values without names, janky order, wants keyboard. Fixed in 0.2.x.
- 2026-11-15 Claude tests v0.2.0-0.2.2 (tools\drive.ps1): all Settings tabs read name+choice, picker works, Exhibition team names, save slots in order, career hub reads.
- 2026-11-15 Claude smoke test v0.1.0: loads, JAWS detected, greeting + 'START GAME' spoken, F2 read 'KOALITY GAME PRESENTS. START GAME.'
- 2026-09-13 (old mod): menus spoke ("Start game. Press A...").





