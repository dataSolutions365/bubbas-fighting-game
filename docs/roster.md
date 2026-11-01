# Roster

These are the 12 fighters from the hand-drawn "Choose Your Fighter" sheet.
Their portraits were cut from that drawing into
`game/Assets/Resources/Roster/`, and the select screen shows all of them.

The animals and special abilities below are guesses made from the drawing.
Change any of them. The same notes are in
`game/Assets/Scripts/Prototype/RosterCatalog.cs`, which the game reads.

| Fighter | Animal | Special idea (from the icon by the portrait) | Playable? |
| --- | --- | --- | --- |
| Jacke | Boxing monkey | Fruit Bowl: eats a snack to heal a little | Yes |
| Keeteeth | Zebra | Sound Blast: a music note shockwave | Needs drawing |
| Esabella | Long-haired fighter | Vine Whip: leaves and vines grab the opponent | Needs drawing |
| PB&J | Twin ponies | Toast Toss: a flying sandwich that hits twice | Needs drawing |
| Phantom Mask | Masked sorcerer (see below) | Smoke Ball: dark smoke slows the opponent | Needs drawing |
| Jewly | Bunny | Gem Shield: a crystal blocks the next hit | Needs drawing |
| Tom | Cat | Nine Lives: gets back up once with a little health | Needs drawing |
| Casey's | Horned owl | Petal Storm: a swirl of flowers pushes the opponent back | Needs drawing |
| Sarena | Maned dragon-horse | Wind Slash: a fast claw streak across the screen | Needs drawing |
| Raven King | Crowned raven | Talon Strike: a diving grab from above | Needs drawing |
| Iraia | Ram | Horn Charge: a running head-butt | Needs drawing |
| Guardian Dragon | Winged wolf-dragon | Guardian Wings: blocks everything for 2 seconds | Needs drawing |

## Phantom Mask

Phantom Mask is the roster's dark sorcerer. His design is our own.

- **Look:** a bone-white, ape-like sorcerer. He wears a cracked mask over his eyes and has glowing purple rune markings down his arms. He also wears a tattered dark sash and a belt buckle with his own symbol. He stands with his hands on his hips.
- **Specials:**
  - **Smoke Ball:** slows the opponent.
  - **Phase Step:** he vanishes into smoke and reappears behind the opponent.
  - **Rune Trap:** a glowing floor circle that roots the opponent.
  - **Shadow Hands:** purple hands that grab the opponent's feet.

## What to draw to make a fighter playable

Each fighter needs the same kind of drawing as the monkey:

1. **Whole body, fighting stance.** Draw the body from the side, facing right, with the feet on the ground. Keep the arms away from the body, so they can be cut out and moved. Draw the legs too, so kicks can be added later.
2. **A punch or special pose.** If you can, draw the same character with one arm thrown forward, or for Phantom Mask, casting with an open hand.
3. **Dark outlines on plain white paper.** Coloured in is best. Take the photo straight on, in good light.

When a drawing comes in, it is cut into body, head and arm pieces, the same way
as `Resources/Monkey/body-parts`. The fighter is then added to the roster in
`PrototypeBootstrap`, and to `firebase/seed/catalog.json`.
