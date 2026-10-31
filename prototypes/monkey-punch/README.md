# Monkey Punch project files

## games/ (open any file in a web browser)
- monkey-punch.html: Punch-Out style test with the cartoon monkey redrawn from the sketch.
- monkey-punch-ring.html: angled ring view, two monkeys walk around and trade punches.
- monkey-punch-original.html: uses the hand-drawn monkey itself, cut into parts (swinging arms, head reactions, gloves-up block).
- monkey-clash.html: side-view fighter. Light/medium/hard punches and kicks, block, swipe to jump and flip, best of 3 rounds.

All games load Phaser 3.80.1 from cdnjs, so they need an internet connection.

## art/ (now in `game/Assets/Resources/Monkey/` so Unity can use it)
- monkey-guard-colored.png, monkey-punch-colored.png: the two drawings, cleaned up, colored, scribble shadows swapped for soft shadows, transparent background.
- body-parts/: the guard drawing cut into separate pieces for animation (full-size 660x971, all pieces line up when stacked).
  - Arm pivots (shoulder): right arm 446,300; left arm 288,300. Neck pivot for head: 362,250.

## Ideas not done yet
- Put the hand-drawn monkey into Monkey Clash (needs legs drawn or cut as separate pieces for kicks).
- Use the son's own colored version once he colors it.
