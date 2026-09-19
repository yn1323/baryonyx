---
name: pixellab-character-prompt
description: >
  Manual-invocation-only skill for turning a character description into a concise
  PixelLab prompt for small JRPG character sprites. Never invoke automatically.
---

# PixelLab Character Prompt

## Invocation policy

- Manual invocation only.
- Use this skill only when the user explicitly invokes it.
- Do not automatically apply this skill just because the conversation is about PixelLab, pixel art, or character design.

## Purpose

Convert the character description supplied together with the invocation into a short English prompt for PixelLab.

PixelLab's Style Reference should do most of the work for overall art direction.
The prompt should identify the character without over-specifying details.

If the source description contains too much information, aggressively remove details.
If it contains too little information, add only the minimum necessary details that are consistent with the character concept.

## Core prompt structure

Build the prompt from these elements:

1. Age range + gender
   - Prefer an age band instead of an exact age.
   - Example: `adult man in his early 30s`
   - Other useful forms: `young adult woman`, `middle-aged man`, `older woman`

2. Hair color + hairstyle
   - Keep this compact.
   - Example: `gray-brown tied-back hair`

3. Occupation / class
   - Example: `scholar mage`
   - Prefer a short, recognizable RPG role.

4. 3-4 visible identifying features
   - Prefer features that affect silhouette, large color blocks, or immediately readable equipment.
   - Good examples:
     - `long blue coat`
     - `large round shield`
     - `spellbook`
     - `broken ring-shaped staff`
     - `red hood`
     - `heavy shoulder armor`

## Color handling

Prevent the generated sprite from becoming unnecessarily muddy, gray, or desaturated.

- Prefer simple, clear base color names for large clothing areas:
  - `blue`, `red`, `green`, `cream`, `gold`, `brown`
- Avoid low-saturation modifiers unless they are essential to the character identity:
  - `muted`
  - `dusty`
  - `smoky`
  - `grayish`
  - `desaturated`
  - `earthy`
  - `weathered`
  - `deep`
  - `dark`
- Do not turn every color into a subtle or realistic shade.
- Keep hair colors accurate to the source description even when they are naturally subdued, such as `gray-brown`.
- Favor clear separation between the character's main color blocks.
- By default, append a short color-quality instruction to the prompt:
  - `clear colors, strong color separation, crisp highlights`
- Do not use words such as `neon`, `extremely vivid`, or `highly saturated` unless the user explicitly wants a flashy palette.
- The goal is colorful, readable SNES-era sprite colors, not modern neon saturation.

## Optional element

Add body type only when it materially helps distinguish the character.

Examples:
- `tall slim`
- `broad-shouldered`
- `petite`
- `muscular`

Do not add body type mechanically to every prompt.

## What to remove

Do not include details that are unlikely to survive at 32x48 logical-pixel scale.

Normally remove:
- personality
- backstory
- motivations
- relationships
- behavioral quirks
- facial micro-details
- wrinkles
- eye color unless absolutely iconic
- tiny jewelry
- stitching
- minor pouches or bottles
- hidden objects
- subtle material descriptions
- exact measurements
- prose explaining why an item exists

Do not try to reproduce every fact from the source text.

## Selection rule for character features

When choosing the 3-4 features, use this priority:

1. silhouette-defining shape
2. main clothing color / large color block
3. primary weapon or held item
4. iconic secondary item
5. only then, smaller accessories

If two details compete, keep the one that remains recognizable when the sprite is viewed very small.

## Prompt style

- Output in English.
- Use a compact comma-separated phrase.
- Prefer approximately 18-40 English words, including the short color-quality instruction.
- Avoid full prose sentences unless necessary.
- Do not repeat pixel-art style instructions inside the character prompt.
- Do not include camera/view instructions unless the user explicitly supplies them as character requirements.
- Do not include explanations before or after the final output.

Example:

`adult male scholar mage in his early 30s, gray-brown tied-back hair, long blue coat, spellbook, broken ring-shaped staff, clear colors, strong color separation, crisp highlights`

## Handling sparse input

If the user provides only a short concept such as:

`盗賊、20代女性、黒髪`

fill in only enough to make a usable sprite concept, for example:

`young adult female thief, short black hair, red hood, charcoal outfit, twin daggers, clear colors, strong color separation, crisp highlights`

Do not invent elaborate lore or many decorative details.

## Handling verbose input

If the user provides a long character sheet, ignore most narrative information and reduce it to:
- age range + gender
- hair color + hairstyle
- class
- the strongest 3-4 visible features

The goal is not completeness.
The goal is a readable character whose design still leaves room for PixelLab and Style Reference to make good visual decisions.

## Required output format

Output exactly these two sections, with no extra commentary:

Prompt

`<concise English PixelLab character prompt>`

Style Description

「32x48に収まるように。全身絵。4頭身。スーファミ後期のドット絵の雰囲気。」
