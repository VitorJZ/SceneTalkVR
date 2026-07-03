import ai2holodeck.constants as constants
constants.LLM_MODEL_NAME = "deepseek-chat"

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import os
import math
import traceback
import json
from typing import List, Dict, Any
from dotenv import load_dotenv

# Load .env variables from the root folder
load_dotenv(dotenv_path="../.env")

from ai2holodeck.generation.holodeck import Holodeck
from ai2holodeck.constants import OBJATHOR_ASSETS_DIR

# Use environmental variables if set, fallback if missing
if not os.environ.get("OPENAI_API_BASE"):
    os.environ["OPENAI_API_BASE"] = "https://models.sjtu.edu.cn/api/v1"
if not os.environ.get("OPENAI_API_KEY"):
    os.environ["OPENAI_API_KEY"] = "sk-AGepmiQRplsPpiBqfA5uUw"

app = FastAPI(title="SceneTalkVR Holodeck Backend")

class SceneRequest(BaseModel):
    environment: str

_holodeck_model = None

@app.on_event("startup")
async def startup_event():
    global _holodeck_model
    print("Pre-loading models... This might take a while.")
    try:
        _holodeck_model = Holodeck(
            openai_api_key=os.environ.get("OPENAI_API_KEY"),
            openai_org=os.environ.get("OPENAI_ORG"),
            objaverse_asset_dir=OBJATHOR_ASSETS_DIR,
            single_room=True
        )
        print("Models loaded successfully!")
    except Exception as e:
        print(f"Error initializing Holodeck: {e}")

@app.post("/generate_scene")
async def generate_scene(request: SceneRequest):
    if _holodeck_model is None:
        raise HTTPException(status_code=500, detail="Model not loaded")

    try:
        scene_template = _holodeck_model.get_empty_scene()
        generated_scene, _ = _holodeck_model.generate_scene(
            scene=scene_template,
            query=request.environment + ". Please place objects at least 2 meters apart from each other.",
            save_dir="./data/scenes",
            generate_image=False,
            generate_video=False,
            use_milp=False
        )
        
        # DEBUG: Print dictionary structure
        print(f"[Backend] Generated scene keys: {list(generated_scene.keys())}")
        
        # Deep search for objects
        raw_objects = generated_scene.get("objects", [])
        if not raw_objects and "scene" in generated_scene:
            raw_objects = generated_scene["scene"].get("objects", [])
        
        filtered_objects = []
        for obj in raw_objects:
            pos = obj.get("position", {})
            # Handle CM vs M and Case Sensitivity
            x = float(pos.get("x", pos.get("X", 0)))
            y = float(pos.get("y", pos.get("Y", 0)))
            z = float(pos.get("z", pos.get("Z", 0)))
            
            # Unit conversion (CM to M)
            if abs(x) > 50 or abs(z) > 50:
                x /= 100.0
                y /= 100.0
                z /= 100.0
            
            # Radius filter (15m)
            dist = math.sqrt(x**2 + y**2 + z**2)
            if dist <= 15.0:
                name = obj.get("object_name") or obj.get("id", "item")
                name = name.split(" (")[0].split("|")[0].split("_")[0].strip()
                
                rot = obj.get("rotation", {})
                ry = float(rot.get("y", rot.get("Y", 0)))
                
                filtered_objects.append({
                    "name": name,
                    "position": [round(x, 3), round(y, 3), round(z, 3)],
                    "rotation": round(ry, 2)
                })
        
        print(f"[Backend] Returning {len(filtered_objects)} objects to Unity.")
        return {
            "environment": request.environment,
            "objects": filtered_objects
        }
        
    except Exception as e:
        print(traceback.format_exc())
        raise HTTPException(status_code=500, detail=str(e))

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8080)
