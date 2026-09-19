---
name: pixellab-animation-prompt
description: >
  Manual-invocation-only skill for creating a character animation sprite sheet
  from an attached reference image, a requested frame count, and an action,
  together with concise frame-by-frame English prompts for PixelLab.
---

# Pixellab Animation Prompt

## Invocation

Use only when the user explicitly invokes `$pixellab-animation-prompt` or asks
to use Pixellab Animation Prompt. Do not automatically invoke it for animation,
PixelLab, or sprite-related requests. Keep `allow_implicit_invocation: false`
in `agents/openai.yaml`.

## Outcome

Create both an actual sprite-sheet image and a copy-ready English animation
prompt. The sheet and prompt must describe the same animation, with exactly the
requested number of frames in the same order. A written plan alone does not
complete the request unless the user explicitly asks for prompts only.

## Inputs and reference inspection

Require these three inputs, accepting information already supplied in the task:

- A readable attached character image or an explicitly identified image file.
- The requested total frame count, as a positive integer.
- The action to animate, such as walking, attacking, casting, or taking damage.

Ask only for missing required inputs or a consequential ambiguity. If the image
is missing or unreadable, ask the user to attach it; do not invent its contents.
If multiple characters or source poses are present and the target is unclear,
ask which to use. Inspect a local image with `view_image` before generation.

Read the reference's silhouette, proportions, palette, clothing, equipment,
facing direction, and camera angle. Preserve these across the animation unless
the requested action explicitly changes them. Preserve weapon handedness and
distinctive details such as a shield, cape, ears, or tail. Do not invent lore,
new equipment, ornaments, or unrelated effects.

Use an explicitly requested per-frame size first, then an established size in
the active task or project. Otherwise preserve the source's logical sprite size
when identifiable; do not confuse an enlarged preview with its logical pixels.
For an illustration without a defined sprite canvas, select a suitable cell size
and state the assumption, asking only if exact dimensions are essential.
Do not import a fixed canvas or body proportion from the character-prompt skill.

## Plan the motion once

Build one ordered sequence of exactly N poses before generating the image or
writing the final prompt. Use that sequence as the shared source for both outputs.
Count the starting pose within N; never silently append a starting or closing
frame. One requested frame means a single pose, not a moving animation.

Choose the action's phases to fit the available frames. For an attack, this may
be preparation, strike, follow-through, and recovery. For walking, preserve the
order of foot contact, weight transfer, and passing poses. With very few frames,
keep the essential readable poses instead of adding frames. With more frames,
use meaningful in-between poses or deliberate holds instead of arbitrary filler.

Honor explicit loop and timing instructions. Otherwise use a loop for recurring
actions such as walking or idle, and a single playback for attacks or reactions.
Use equal frame durations unless the user specifies otherwise; an intentional
hold occupies actual frames within N. For a loop, make the last-to-first motion
continuous without automatically duplicating the first frame. For a single
playback, end in the action's appropriate result or recovery pose; do not force
a defeated character back to standing.

Keep the camera fixed and the character's scale consistent. Keep the character
anchored in place for ordinary locomotion sprites unless travel is requested;
allow intentional vertical motion, recoil, or lunges required by the action.
Avoid accidental sliding, mirrored equipment, or frame-to-frame changes in
body size. Follow visible support feet, weapon arcs, and secondary motion so
adjacent frames connect plausibly. Keep tiny-sprite actions readable rather
than adding many simultaneous movements.

## Write the English animation prompt

Start with a compact shared instruction identifying the reference, action,
frame count, facing direction, and loop or single playback. State any essential
invariants once. Do not repeat a character sheet, backstory, or long art-style
instructions in every frame.

Then write exactly N numbered lines, from `Frame 01:` through the last frame.
Each line should describe one visible pose in concise, concrete English, usually
12-25 words. Use as many words as needed for an unambiguous movement.

Describe the torso, weight or support foot, and the relevant arm, leg, or weapon
position. Include secondary motion only when it helps the action. Describe a
specific moment rather than several successive actions inside one frame.
Use character-relative left/right consistently; use screen-left/screen-right
when describing an image-space direction. Avoid vague phrases such as
"moves dynamically", "continues the action", or "same as before".

For example, a pose may read:
`Frame 03: The torso leans forward, the front foot plants, and the sword arm extends through a downward slash.`

Put the shared instruction and all numbered frame lines together in one
copy-ready text block. Keep Japanese explanation, sheet layout directions,
citations, and tool details outside that block. These lines are natural-language
PixelLab prompt guidance, not a claim that PixelLab enforces exact per-frame
control or accepts an undocumented API field.

## Generate and verify the sprite sheet

Use the available `imagegen` skill and built-in `image_gen` tool to generate the
sheet from the supplied reference and the same ordered pose sequence. Include
the actual image reference, not just a text description of it. Follow the current
tool schema for local file paths or recent conversation images.

Request an evenly spaced grid of identical cells, one complete pose per occupied
cell, transparent background, consistent pixel treatment and palette, and no
labels, numbers, borders, scenery, or watermarks. Keep the character and equipment
inside every cell, with a consistent origin and enough clearance for the action.
Use the requested layout if supplied. Otherwise choose a compact rectangular
grid and state its columns and rows. Read frames left-to-right, then top-to-bottom.
If the grid has unused cells, leave only the trailing cells empty and transparent;
they do not count as animation frames.

Inspect the generated image before presenting it. Check the occupied-frame count,
order, pose-to-text agreement, source likeness, handedness, clipping, alignment,
and loop continuity where relevant. Check actual file dimensions and transparency
with read-only image inspection when a file is available. Do not claim exact
logical pixel dimensions or successful motion playback based on the prompt alone.

If a material mismatch is visible, make one focused correction and recheck. If
the result still has a material limitation, show it as a draft and state the
specific mismatch briefly; never label an unchecked or incorrect grid as ready
for import. Do not merely rewrite the text to excuse an incorrect animation.

This skill creates an image and prompts for the user to use in PixelLab. It does
not authorize opening or operating PixelLab through Computer Use or Browser Use.
Use those only when separately instructed. Do not claim the image was generated
or tested in PixelLab when it was created with another tool. If image generation
is unavailable, provide the English prompt and clearly state that the sheet
could not be created; do not substitute a text-only grid for an image.

## Final output

Keep the final response to these two sections, without a preamble:

### Sprite Sheet

Show the generated sheet inline, with a usable file link when available. Add one
short Japanese caption giving N frames, the grid layout, playback order, and loop
or single playback. State actual cell size only when verified. Mention any material
limitation here. If prompts only were explicitly requested, omit this section.

### Animation Prompt

Provide one copy-ready English text block containing the shared instruction and
exactly N frame descriptions. Do not split each frame into a separate block or
put the frame descriptions in a table.
