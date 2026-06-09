# Patch Holodeck constants to use SJTU model before other imports
import ai2holodeck.constants as constants
constants.LLM_MODEL_NAME = "deepseek-chat"

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import os
import math
import traceback
from typing import List, Dict, Any

# Import ai2holodeck components
from ai2holodeck.generation.holodeck import Holodeck
from ai2holodeck.constants import OBJATHOR_ASSETS_DIR

# Set SJTU API Configuration
os.environ["OPENAI_API_BASE"] = "https://models.sjtu.edu.cn/api/v1"
os.environ["OPENAI_API_KEY"] = "sk-AGepmiQRplsPpiBqfA5uUw"

app = FastAPI(title="SceneTalkVR Holodeck Backend")

class SceneRequest(BaseModel):
    environment: str

_holodeck_model = None

@app.on_event("startup")
async def startup_event():
    global _holodeck_model
    print("Pre-loading models (CLIP, SentenceTransformer)... This might take a while.")
    openai_api_key = os.environ.get("OPENAI_API_KEY")
    openai_org = os.environ.get("OPENAI_ORG")
    
    try:
        _holodeck_model = Holodeck(
            openai_api_key=openai_api_key,
            openai_org=openai_org,
            objaverse_asset_dir=OBJATHOR_ASSETS_DIR,
            single_room=True
        )
        print("Models loaded successfully!")
    except Exception as e:
        print(f"Error initializing Holodeck during startup: {e}")

def get_model():
    return _holodeck_model

@app.post("/generate_scene")
async def generate_scene(request: SceneRequest):
    model = get_model()
    if model is None:
        raise HTTPException(
            status_code=500, 
            detail="Holodeck model not initialized. Check server startup logs."
        )

    try:
        # 1. Get empty scene template
        scene_template = model.get_empty_scene()
        
        # 2. Generate scene using ai2holodeck logic
        # Note: we disable image/video generation for speed
        generated_scene, _ = model.generate_scene(
            scene=scene_template,
            query=request.environment,
            save_dir="./data/scenes",
            generate_image=False,
            generate_video=False,
            add_ceiling=False,
            add_time=True,
            use_constraint=False,
            use_milp=False,
            random_selection=False
        )
        
        # 3. Filter and Format objects
        # Target: Within 3 meters of (0,0,0)
        filtered_objects = []
        for obj in generated_scene.get("objects", []):
            pos = obj.get("position", {"x": 0, "y": 0, "z": 0})
            x, y, z = pos.get("x", 0), pos.get("y", 0), pos.get("z", 0)
            
            # Calculate 3D Euclidean distance
            distance = math.sqrt(x**2 + y**2 + z**2)
            
            if distance <= 3.0:
                # Use a friendly name if possible, otherwise clean up the ID
                name = obj.get("object_name")
                if not name:
                    raw_id = obj.get("id", "Unknown")
                    # Clean up ID like "Dining Table (Living Room)" or "door|1|..."
                    name = raw_id.split(" (")[0].split("|")[0].split("_")[0].strip()
                
                # Get Y rotation (yaw)
                rotation_y = obj.get("rotation", {}).get("y", 0)
                
                filtered_objects.append({
                    "name": name,
                    "position": [round(x, 3), round(y, 3), round(z, 3)],
                    "rotation": round(rotation_y, 2)
                })
        
        return {
            "environment": request.environment.replace(" ", "_"),
            "objects": filtered_objects
        }
        
    except Exception as e:
        print(traceback.format_exc())
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/health")
async def health():
    return {"status": "ok", "model_loaded": _holodeck_model is not None}

if __name__ == "__main__":
    import uvicorn
    # Use port 8000 as default for FastAPI
    uvicorn.run(app, host="0.0.0.0", port=8000)
