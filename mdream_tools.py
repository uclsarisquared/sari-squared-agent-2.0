import moondream as md
from PIL import Image

# Initialize with your API key
model = md.vl(api_key="secret")

# Load an image
image = Image.open("C:\\Sari\\sari-squared-agent-2.0\\currentview\\current_view.jpg")

def point_at_object(object: str):
    result = model.point(image, object)
    point = result["points"][0]
    # log the point for debugging
    return (int(point["x"]*100), int(point["y"]*100))