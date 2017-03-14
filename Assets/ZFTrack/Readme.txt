  _____               _
 |_   _| __ __ _  ___| | _____
   | || '__/ _` |/ __| |/ / __|
   | || | | (_| | (__|   <\__ \
   |_||_|  \__,_|\___|_|\_\___/

Tacks! In your games! Using physics!

Copyright 2015, 2016 Zen Fulcrum LLC
https://www.assetstore.unity3d.com/en/#!/content/33512

Need help? support@zenfulcrum.com

===========
Quick Start
===========

To make a track:
	Copy/import the ZFTrack folder into your project.
	In the Hierarchy view: Create -> 3D Object -> Track
	Grab the track and tilt it so it's pointing slightly downward.

To add a cart to a track:
	Add a cube. Click "Add Component", search for "Track Cart" and add it to the cube.
	Move the cube so it's roughly near the start of the track.
	Under the "Track Cart" component click "Find and Snap to Track" to attach to the nearest track and move the cart into place.
	Hit play. Watch the cart slide down the track and fall off.

Congratulations! You've made your first track and cart!

============
Going deeper
============

- At the end of the selected track section track there's a cone. Click it to add a new connected track section or to select the next track section.
- At the end of the selected track section there's a cylinder. Click it, then use the controls that appear to move that node in the track.
	- This will automatically move linked tracks too.
	- For best results, set your object pivot to "center" in the editor. This will keep the track bending controls and the object's normal controls from overlapping.
- There's lots of tools to help you shape and link tracks, see the "Bending" section below.
- You can find full explanations for each property (and what it does) near the top of Track.cs and TrackCart.cs
- For carts to properly slide along the track, you must both align the track and link the track pieces together.
	- Most of this will be done for you with the included editor tools, but more advanced effects can be accomplished, see Directionality below.
- You can change the appearance of the track and even create your own track template. See Custom Meshes below.
- You can merge adjacent linked tracks. Select the pieces, then click "Merge" in the inspector. If it's not enabled, all selected items aren't track, linked, and adjacent. Merging loops is also not allowed.
- You can easily multi-select adjacent track sections by holding shift while clicking "Select Next" or "Select Prev" in the inspector. Select an entire coaster by clicking "Select Connected."
- You can add sounds to the cart, read below in "Cart Sounds."
- Take a look at the Prefabs folder for example carts (with sound) and non-default track profiles.
- Attach the WheelSpin script to your cart's wheels to make them spin as the cart moves. Be sure to input the correct wheel radius.
- Sources.zip contains some source Blender/GIMP/Audacity project files if you're looking to make changes and happen to use these programs.

=======
Bending
=======

- When a track is selected, you'll see some tools in the inspector:
	- The first row contains select/create buttons to select adjacent track sections and to create new sections, similar to clicking on the end cones. (Shift+click to multi-select.)
	- The second row contains snap buttons. If you move a track manually or want to connect unrelated ranges, use these tools to snap track sections together.
		- If the current track is linked, the linked track is brought together.
		- If the current track is not linked, the nearest unlinked track is found and they are brought together and linked.
	- Next, there's a button labeled "Track Bender". Click it to open the track bending window, described in more detail below.
	- Finally, on the last row there's:
		- "Split" which will split the current track segment into two segments, allowing more control over the curve.
		- "Unlink" which will disconnect the current track from adjacent track pieces. You can also unlink individual sides by deleting the value for "Next Track" or "Prev Track" up in the main part of the inspector.
		- "Select Connected" will select all linked track sections, allowing you to easily move an entire roller coaster or set of track at once.

- There's also a track bending window.
	- This window is designed to help you quickly rough out a track layout. You can't control track as finely as you can by grabbing handles in the editor, but it should help you get everything laid out a bit more quickly before you get to fine-tuning.
	- Access it with Window -> "Track Bender" or by clicking "Track Bender" in the inspector of a selected track piece.
	- Once a single track piece is selected, controls will appear. The tools are divided into roughly four sections:
		- Section 1: Bending
			- The first 9 buttons help aim the track.
			- Roll left, up, roll right, left, right, down - these change the direction of the track section. Resulting angles are quantized to global world-aligned rotations.
			- "Build" with build the next section of track. If the next section already exists it will be selected.
			- "Straight" will make the current track piece perfectly straight, with no bend or roll.
			- "Flat" levels the end of the track section with respect to the world's down and quantizes its direction. The opposite end is not moved, just re-angled. This is useful for getting back to *exactly* flat after doing a series of twists and turns.
		- Section 2: Navigation
			- "Unbuild" will delete the current track piece and select the piece before it, if any.
			- "Shorter" and "Longer" will shorten and lengthen the current track section.
			- "Reverse" will toggle the working direction. When its selected, bending & friends will act on the start of a section; build & friends will work in the opposite direction.
			- Previous track ("<<"") and next track (">>") select the previous or next track. Shift-click to multi-select track sections.
		- Section 3: Sliders
			- There are four sliders you can use to more finely adjust the bending done in the first section.
			- "Roll", "Left/Right", "Up/Down" each represent a component of a world-aligned Euler rotation used to construct the track section's curve. If you run out of room to get the bend you want, just drag the slider to the other side and continue.
			- "Length" adjusts the length of the generated curve.
			- Note that it's not possible to create every possible track curve using these sliders. Use the handles in the scene views to finalize details.
		- Section 4: Options
			- "Align View" allows you to select a scene view that will focus on tracks. Once you've select a view any time a track is selected or bent the view will update to show that track. This makes it a bit easier to see things from the cart's perspective.
			- "Snap Angle" determines the angles at which the main buttons (Section 1: Bending) and "Flat" button will snap or quantize to.
			- "Snap Length" determines the distances at which the Shorter/Longer buttons will snap.


=================
Tips/Info/Gotchas
=================

- To simulate friction, increase the "Drag" value on your cart's rigidbody.
- If you want a mostly frictionless cart, try setting the cart's "Curve Preservation" to about 0.7.
- If you want to rearrange a section of track, select the relevant pieces and move them normally. Then, use the snap/snap to buttons in the inspector to reconnect any junctions that got broken.
- Scaling is not presently supported.
- To make a track visible, but intangible, disable the mesh collider component.
- If your cart suddenly vanishes to the aether when you start, make sure it's positioned close to the track and that it's pointing the right direction. The physics system usually does a pretty good job with error, but if a cart's pointed the wrong way it tends to go flying.
- Carts will not collide with their current track or adjacent tracks. You don't need to worry about them hitting each other and can even have the cart physically intersect the track.
- Tracks can be bent, moved, disconnected and reconnected during runtime via scripts. Even tracks with carts on them can be moved/bent a reasonable amount.
- Read through the documentation for track properties in Track.cs and cart properties in TrackCart.cs. Some highlights:
	- A cart's current track is found in the "Current Track" property and will need to be set before it will slide along a track. (The "Find and Snap to Track" button will do this for you.)
	- Tracks can accelerate or brake their carts. Look at "Acceleration" and "Brakes" in Track.cs.
	- To increase or decrease the resolution of the generated track mesh, chance "Resolution" on a track.
	- To have more or fewer ties, change the "Tie Interval".
- The cart's path is always calculated as smoothly as possible, irrespective of the rendering resolution.
- The cart will follow the path centered on the track. To make a cart sit on top of or hover over a track, adjust the cart model to be higher in its origin or put the renderers/colliders as children to the cart with an offset.
- If you undo a delete and some of the tracks don't seem linked right, save your scene to fix it. There's an obscure Unity bug that causes the inspector and the actual objects to get out-of-sync. :-(


=============
Custom Meshes
=============

This plugin will automatically create the necessary meshes for tracks you build. It does this using a track template.

A few templates are included with this plugin. They are located in ZFTrack/Models. The source files can be found in Sources.zip.

To change the look of a track, assign new meshes to the "Rail Mesh" and "Tie Mesh" properties on the track. Either or both may be null. Set both to null for invisible, intangible tracks!

A track template consist of two parts:

- The main track profile, the Rail Mesh. This is extruded along the path of the track to create the primary rail.
	- To create your own:
		- Create a segment of track that follows the straight line from (0, 0, -1) to (0, 0, 1).
		- All polygons that cross the the origin will be used for the main track path. All other polygons will be used for end caps.
	- When this mesh is assigned to a rail, the following will happen:
		- Any polygon that crosses the XY plane will be repeated along a curve to build rails.
		- Any polygon that does not cross the XY plane will be used for end caps. The plugin will automatically detect which end caps go where.

- The second piece is the rail ties mesh, the Tie Mesh. This is repeated at intervals to add rail ties/interconnects/cross bars along the track.
	- It is copied unmodified into position.
	- To create your own, create a rail tie, or any other decoration you'd like, centered on the origin (0, 0, 0).

Either of the track meshes, Rail Mesh or Tie Mesh, may be null. The system will simply omit polygons for that aspect of the track. Additionally, you may omit end caps or rails in your Rail Mesh if you so desire.

End caps are only added when track is unlinked, that is, the track section is an end piece. You can force end cap addition by ticking the "Force End Caps" checkbox in the inspector.

Note: coordinates given are in Unity space. Many editors use a different coordinate system. For things to function properly, the coordinates must be correct after being imported to Unity, which may require rotating your source model or exporting with different options. The model needs to be aligned along the z axis.

Also note: Unity sometimes imports .fbx models at 0.01 scale (which makes absolutely no sense and ought to be fixed). Make sure that the "scale" for any imported .fbx models is set to 1 unless you have something else in mind.



==============
Directionality
==============

Each track segment has an intrinsic direction.

The editor interface will help you set up a track the canonical way: all track segments facing the same direction. For most use cases this is all you need.

If you need to do something more advanced, such as what you can see in the "Reversal" scene in Demo folder, you will have to link the tracks manually using the "Next Track" and "Prev Track" properties.

Information:
- Every track segment has a previous (Prev Track) and a next (Next Track).
- For a cart to travel smoothly, the tracks need to be physically aligned AND be linked together.
- When a cart reaches the end of a track, it try to continue onto the specified next/prev section. Carts will automatically "reverse" direction when traveling across two tracks that point different ways.
- The track prev/next topology does not need to be "normal". You can have tracks that connect to things that don't connect back.
	- Note, however, if things don't connect when going backwards, the cart will be unable to travel backwards.


===========
Cart Sounds
===========

To add sounds to your cart add the "TrackCartSound" script to your cart.

The TrackCartSound will play a combination of sounds based on the cart's current speed. It will also automatically start/stop clips and adjust the volume and pitch of the sounds to match the cart's speed. It will also increase the volume of the sounds as the cart rounds corners and valleys, giving the cart a more believable sound.


Typically you want sound clips for one or more of the following:
	- An "idling" noise for when the cart is stopped
	- The sound of the cart traveling at a slow speed.
	- The sound of the cart traveling at a medium speed.
	- The sound of the cart traveling at a fast speed.

Any of the above are optional, and you can add more clips if you'd like. Make sure your clips loop well!

You may find it helpful to use the included "CartSoundTest" scene found with the demos.

Start by opening up CartSoundTest. Delete the cart that's there and drop in your cart prefab. Add a Track Cart Sound component to it if you haven't already.

Set the "Max Speed" value to the highest speed (in world units/sec) you think the cart will ever go. If your cart ever goes faster than this, TrackCartSound will play sounds as if it was only traveling at this maximum speed.

On the Track Cart Sound component click "Add".

Each clip has an associated reference speed and volume vs. speed curve. The reference speed is used to calculate the correct pitch for a sound, based on the speed. The curve is used to map out how loud a given clip is at a given volume.

Use the interface to set up your first clip. Assign the clip to the Clip field. Your sound clip was recorded with a particular cart speed in mind. This is the clip's "Reference Speed" represented here as a fraction of the maximum speed. The Speed Scale determines how much the clip's pitch changes with speed. Fill these fields out with your best guesses.

Repeat the previous two steps to add all the clips you need.

When you are done click "Reset All Curves". This will adjust the all the volume curves to reasonable defaults based on what you told it.

Hit play! Use the sliders in the CartSoundTest scene to adjust the speed of the cart and listen to how things sound. Use the tips below to refine the audio. When you are done, save your prefab and toss it in your scene!

If things need tuning, here's some things you can tweak on the clips:
	- The reference speed is the speed at which the cart must be moving to play the sound as it was recorded, with no pitch change. The reference speed is expressed as a percentage of the maximum (sound) speed.
	- If the reference speed is zero, the sound's pitch will always be normal.
	- The "Speed Scale" can be used to give more or less pitch bending to a clip. At the default value of 1, a cart traveling at twice the reference speed will have double the pitch (one octave higher).
	- "Volume vs. Speed" shows an overview of all the sounds clips, their reference speeds, and respective volumes across the speed range.
		- The x-axis is speed. It goes from 0 on the left to your chosen max speed on the right.
		- The y-axis is volume. It goes from muted on the bottom to AudioClip.volume = 1 at the top.
		- Vertical lines indicate reference speeds.
	- To adjust the curve for a clip, click the "Volume Curve" below the graph.
	- Leave some room for the volume to get louder as the cart curves around the track. Try not to put your curves higher than 50%-70% of the maximum volume.
	- Typically, you will want to increase the volume with speed. Adjust the parameters until things sound as you would expect.


===============================
Tracks on Tracks (experimental)
===============================

You can give track a rigidbody and slide carts along them (or them along carts!). If you do, we try to correctly emulate physics. Not every detail works perfectly, but you can still get some pretty cool effects.

Check out the LiftRide demo for an example.

Here's some things that can go wrong:
	- Cart meshes are almost always concave. Unity doesn't like concave moving meshes. Mark the mesh collider as convex to get a slightly-reasonable-ish collider to use.
	- Newton's third law of motion is in full force and effect. If you accelerate a cart forward, be prepared for the track to go flying backward.
	- Place multiple track sections under a single GameObject with a single Rigidbody to have the sections move as a single, solid piece.
	- Attached carts on moving tracks don't report the correct velocity via the physics API. Why? ¯\_(ツ)_/¯ Use cart.GetVelocityOnTrack(cart.LastTangent) to get a more accurate velocity vector relative to the track.


=========
Scripting
=========

Documentation for various properties and functions can be found in the various classes. Some example scripts are included with the demos.

