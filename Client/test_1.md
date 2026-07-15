❯ make run-app
xvfb-run -a /home/spring5/miniconda3/envs/holodeck/bin/python -m uvicorn app:app --host 0.0.0.0 --port 8080
INFO:     Started server process [4584]
INFO:     Waiting for application startup.
Pre-loading models (CLIP, SentenceTransformer)... This might take a while.
Models loaded successfully!
INFO:     Application startup complete.
INFO:     Uvicorn running on <http://0.0.0.0:8080> (Press CTRL+C to quit)
User: You are an experienced room designer. Please assist me in crafting a floor plan. Each room is a rectangle. You need to define the four coordinates and specify an appropriate design scheme, including each room's color, material, and texture.
Assume the wall thickness is zero. Please ensure that all rooms are connected, not overlapped, and do not contain each other.
Note: the units for the coordinates are meters.
For example:
living room | maple hardwood, matte | light grey drywall, smooth | [(0, 0), (0, 8), (5, 8), (5, 0)]
kitchen | white hex tile, glossy | light grey drywall, smooth | [(5, 0), (5, 5), (8, 5), (8, 0)]

Here are some guidelines for you:

1. A room's size range (length or width) is 3m to 8m. The maximum area of a room is 48 m$^2$. Please provide a floor plan within this range and ensure the room is not too small or too large.
2. It is okay to have one room in the floor plan if you think it is reasonable.
3. The room name should be unique.

Now, I need a design for cyberpunk cafe.
Additional requirements: I only need one room.
Your response should be direct and without additional text at the beginning or end.

AI: Here is the floor plan:
cyberpunk cafe | polished concrete, dark grey | neon blue LED panels, smooth | [(0, 0), (0, 6), (5, 6), (5, 0)]
AI: The floor plan is valid. (Only one room)

User: I am now designing cyberpunk cafe. Please help me decide the wall height in meters.
Answer with a number, for example, 3.0. Do not add additional text at the beginning or in the end.

AI: The wall height is 4.5

User: I need assistance in designing the connections between rooms. The connections could be of three types: doorframe (no door installed), doorway (with a door), or open (no wall separating rooms). The sizes available for doorframes and doorways are single (1m wide) and double (2m wide).

Ensure that the door style complements the design of the room. The output format should be: room 1 | room 2 | connection type | size | door style. For example:
exterior | living room | doorway | double | dark brown metal door
living room | kitchen | open | N/A | N/A
living room | bedroom | doorway | single | wooden door with white frames

The design under consideration is cyberpunk cafe, which includes these rooms: cyberpunk cafe. The length, width and height of each room in meters are:
cyberpunk cafe: 5.0 m x 6.0 m x 4.5 m

Certain pairs of rooms share a wall: (exterior, cyberpunk cafe). There must be a door to the exterior.
Adhere to these additional requirements: N/A.
Provide your response succinctly, without additional text at the beginning or end.

AI: Here is the doorway plan:
exterior | cyberpunk cafe | doorway | double | dark gray metal door with neon trim
There are multiple ways between exterior and cyberpunk cafe

User: Guide me in designing the windows for each room. The window types are: fixed, hung, and slider.
The available sizes (width x height in cm) are:
fixed: (92, 120), (150, 92), (150, 120), (150, 180), (240, 120), (240, 180)
hung: (87, 160), (96, 91), (120, 160), (130, 67), (130, 87), (130, 130)
slider: (91, 92), (120, 61), (120, 91), (120, 120), (150, 92), (150, 120)

Your task is to determine the appropriate type, size, and quantity of windows for each room, bearing in mind the room's design, dimensions, and function.

Please format your suggestions as follows: room | wall direction | window type | size | quantity | window base height (cm from floor). For example:
living room | west | fixed | (130, 130) | 1 | 50

I am now designing cyberpunk cafe. The wall height is 450 cm. The walls available for window installation (direction, width in cm) in each room are:
cyberpunk cafe: north, 500 cm; east, 600 cm; south, 500 cm;

Please note: It is not mandatory to install windows on every available wall. Within the same room, all windows must be the same type and size.
Also, adhere to these additional requirements: Only one wall of each room should have windows.

Provide a concise response, omitting any additional text at the beginning or end.

AI: Here is the window plan:
cyberpunk cafe | north | fixed | (240, 180) | 2 | 90

AI: Selecting objects for cyberpunk cafe...

AI: Here is the object selection plan:
{'cyberpunk cafe': {'cyberpunk_booth_sofa': {'description': 'curved, black faux-leather booth sofa with neon cyan piping and chrome legs', 'location': 'floor', 'size': [250, 120,
90], 'quantity': 1, 'variance_type': 'same', 'objects_on_top': [], 'object_name': 'cyberpunk_booth_sofa'}, 'bar_counter': {'description': 'sleek, dark metallic bar counter with embedded led strip lighting and a glossy black top', 'location': 'floor', 'size': [300, 60, 110], 'quantity': 1, 'variance_type': 'same', 'objects_on_top': [], 'object_name': 'bar_counter'}, 'neon_wall_panel': {'description': 'large hexagonal led wall panel with animated cyberpunk cityscape and pink/blue neon glow', 'location': 'wall', 'size': [200, 150, 10], 'quantity': 1, 'variance_type': 'same', 'objects_on_top': [], 'object_name': 'neon_wall_panel'}}}
completion text for cyberpunk cafe:
[
    {
        "object_name": "cyberpunk_booth_sofa-0",
        "position": {"X": 150, "Y": 200},
        "rotation": 0
    },
    {
        "object_name": "bar_counter-0",
        "position": {"X": 350, "Y": 400},
        "rotation": 90
    }
]

wall object constraint plan for cyberpunk cafe:
neon_wall_panel-0 | N/A | 65

Symlink from /home/spring5/.objathor-assets/2023_09_23/assets/5be5f5ee8a2946749368e86e2975c415 to /home/spring5/.ai2thor/releases/thor-Linux64-3213d486cd09bcbafce33561997355983bdf8d1a/processed_models/5be5f5ee8a2946749368e86e2975c415
Last Action: CreateRuntimeAsset
Symlink from /home/spring5/.objathor-assets/2023_09_23/assets/8c935a5332e1470bba802f50f9e918e3 to /home/spring5/.ai2thor/releases/thor-Linux64-3213d486cd09bcbafce33561997355983bdf8d1a/processed_models/8c935a5332e1470bba802f50f9e918e3
Last Action: CreateRuntimeAsset
Only one room in the scene. Assigning the room to Procedural0.
INFO:     127.0.0.1:54731 - "POST /generate_scene HTTP/1.1" 200 okay
