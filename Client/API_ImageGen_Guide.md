My API Key: sk-azcoikjkgvfzqcpytdlvjnemnxnvcqnhghlnsvtgfugoqblv

AuthorizationBearer <token>required

Use the following format for authentication: Bearer[](https://cloud.siliconflow.cn/account/ak)

In: `header`

modelstringrequired

Corresponding Model Name. To better enhance service quality, we will make periodic changes to the models provided by this service, including but not limited to model on/offlining and adjustments to model service capabilities. We will notify you of such changes through appropriate means such as announcements or message pushes where feasible. **For a complete list of available models, please check the [Models](https://cloud.siliconflow.cn/models?types=to-image)**.

Example`"Qwen/Qwen-Image-Edit-2509"`

promptstringrequired

Example`"an island near sea, with seagulls, moon shining over the sea, light house, boats int he background, fish flying over the sea"`

negative\_promptNegative Prompt

negative prompt

image\_sizeImage size, format is \[width\]x\[height\].

Image resolution in "widthxheight" format (Required). To ensure optimal quality, using the recommended values for your model is strongly advised. **Qwen/Qwen-Image-Edit-2509 and Qwen/Qwen-Image-Edit not support this field.** Recommended Values:

- For Kolor model:
  - "1024x1024" (1:1)
  - "960x1280" (3:4)
  - "768x1024" (3:4)
  - "720x1440" (1:2)
  - "720x1280" (9:16)
- For Qwen-Image model:
  - "1328x1328" (1:1)
  - "1664x928" (16:9)
  - "928x1664" (9:16)
  - "1472x1140" (4:3)
  - "1140x1472" (3:4)
  - "1584x1056" (3:2)
  - "1056x1584" (2:3)

batch\_sizeNumber Images

number of output images. Only applicable to Kwai-Kolors/Kolors.

Default`1`

Range`1 <= value <= 4`

num\_inference\_stepsNumber Inference Steps

number of inference steps

Default`20`

Range`1 <= value <= 100`

guidance\_scaleGuidance Scale

This value is used to control the degree of match between the generated image and the given prompt. The higher the value, the more the generated image will tend to strictly match the text prompt. The lower the value, the more creative and diverse the generated image will be, potentially containing more unexpected elements. Only applicable to Kwai-Kolors/Kolors.

Default`7.5`

Range`value <= 20`

cfgCFG Scale

CFG (Classifier-Free Guidance) is a technique that adjusts how closely generated outputs follow input prompts by balancing precision and creativity. This field is only applicable to Qwen/Qwen-Image models. For text generation scenarios, the CFG value must be greater than 1. The official configuration uses 50 steps with CFG 4.0. When CFG is set too small, it becomes nearly impossible to generate text.

imageUpload Image

The image used for uploading an image can be in base64 format or a URL.

Value in`"data:image/png;base64, XXX" | "img_url"`

Example`"https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641"`

image2Upload Image

The image used for uploading an image can be in base64 format or a URL. This field is only applicable to Qwen/Qwen-Image-Edit-2509.

Value in`"data:image/png;base64, XXX" | "img_url"`

Example`"https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641"`

image3Upload Image

The image used for uploading an image can be in base64 format or a URL. This field is only applicable to Qwen/Qwen-Image-Edit-2509.

Value in`"data:image/png;base64, XXX" | "img_url"`

Example`"https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641"`

## [Response Body](https://api-docs.siliconflow.cn/docs/api/images-generations-post#response-body)

The response from the model. The response header contains the x-siliconcloud-trace-id field, which serves as a unique identifier for tracing requests, facilitating log queries and issue troubleshooting.

TypeScript Definitions

Use the response body type in TypeScript.

```
curl --request POST \
  --url https://api.siliconflow.cn/v1/images/generations \
  --header 'Authorization: Bearer YOUR-API-KEY' \
  --header 'Content-Type: application/json' \
  --data '{
  "model": "Kwai-Kolors/Kolors",
  "prompt": "an island near sea, with seagulls, moon shining over the sea, light house, boats int he background, fish flying over the sea",
  "image_size": "1024x1024",
  "batch_size": 1,
  "num_inference_steps": 20,
  "guidance_scale": 7.5
}'
```

```
import requests
url = "https://api.siliconflow.cn/v1/images/generations"
payload = {
    "model": "Kwai-Kolors/Kolors",
    "prompt": "an island near sea, with seagulls, moon shining over the sea, light house, boats int he background, fish flying over the sea",
    "image_size": "1024x1024",
    "batch_size": 1,
    "num_inference_steps": 20,
    "guidance_scale": 7.5
}
headers = {
    "Authorization": "Bearer YOUR-API-KEY",
    "Content-Type": "application/json"
}
response = requests.request("POST", url, json=payload, headers=headers)
print(response.text)
```

```
const options = {
  method: 'POST',
  headers: {
    Authorization: 'Bearer YOUR-API-KEY',
    'Content-Type': 'application/json'
  },
  body: '{"model":"Kwai-Kolors/Kolors",
  "prompt":"an island near sea, with seagulls, moon shining over the sea, light house, boats int he background, fish flying over the sea",
  "image_size":"1024x1024",
  "batch_size":1,
  "num_inference_steps":20,
  "guidance_scale":7.5
};

fetch('https://api.siliconflow.cn/v1/images/generations', options)
  .then(response => response.json())
  .then(response => console.log(response))
  .catch(err => console.error(err));
```

```
curl --request POST \
  --url https://api.siliconflow.cn/v1/images/generations \
  --header 'Authorization: Bearer YOUR-API-KEY' \
  --header 'Content-Type: application/json' \
  --data '{
  "model": "Kwai-Kolors/Kolors",
  "prompt": "an island near sea, with seagulls, moon shining over the sea, light house, boats int he background, fish flying over the sea",
  "image_size": "1024x1024",
  "batch_size": 1,
  "num_inference_steps": 20,
  "guidance_scale": 7.5,
  "image": "https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641"
}'
```

```
import requests
url = "https://api.siliconflow.cn/v1/images/generations"
payload = {
    "model": "Kwai-Kolors/Kolors",
    "prompt": "an island near sea, with seagulls, moon shining over the sea, light house, boats int he background, fish flying over the sea",
    "image_size": "1024x1024",
    "batch_size": 1,
    "num_inference_steps": 20,
    "guidance_scale": 7.5,
    "image": "https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641"
}
headers = {
    "Authorization": "Bearer YOUR-API-KEY",
    "Content-Type": "application/json"
}
response = requests.request("POST", url, json=payload, headers=headers)
print(response.text)
```

```
const options = {
  method: 'POST',
  headers: {
    Authorization: 'Bearer YOUR-API-KEY',
    'Content-Type': 'application/json'
  },
  body: '{"model":"Kwai-Kolors/Kolors",
  "prompt":"an island near sea, with seagulls, moon shining over the sea, light house, boats int he background, fish flying over the sea",
  "image_size":"1024x1024",
  "batch_size":1,
  "num_inference_steps":20,
  "guidance_scale":7.5
  "image":"https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641"}'
};

fetch('https://api.siliconflow.cn/v1/images/generations', options)
  .then(response => response.json())
  .then(response => console.log(response))
  .catch(err => console.error(err));
```

```
curl --request POST \
  --url https://api.siliconflow.cn/v1/images/generations \
  --header 'Authorization: Bearer YOUR-API-KEY' \
  --header 'Content-Type: application/json' \
  --data '{
  "model": "Qwen/Qwen-Image-Edit-2509",
  "prompt": "an island near sea, with seagulls, moon shining over the sea, light house, boats int he background, fish flying over the sea",
  "num_inference_steps": 20,
  "cfg": 4,
  "image": "https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641",
  "image2": "https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641",
  "image3": "https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641"
}'
```

```
import requests

url = "https://api.siliconflow.cn/v1/images/generations"

payload = {
    "model": "Qwen/Qwen-Image-Edit-2509",
    "prompt": "an island near sea, with seagulls, moon shining over the sea, light house, boats int he background, fish flying over the sea",
    "num_inference_steps": 20,
    "cfg": 4,
    "image": "https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641",
    "image2": "https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641",
    "image3": "https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641"
}
headers = {
    "Authorization": "Bearer YOUR-API-KEY",
    "Content-Type": "application/json"
}

response = requests.request("POST", url, json=payload, headers=headers)
print(response.text)
```

```
const options = {
  method: 'POST',
  headers: {
    Authorization: 'Bearer YOUR-API-KEY',
    'Content-Type': 'application/json'
  },
  body: '{"model":"Qwen/Qwen-Image-Edit-2509",
  "prompt":"an island near sea, with seagulls, moon shining over the sea, light house, boats int he background, fish flying over the sea",
  "num_inference_steps":20,
  "cfg":4,
  "image":"https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641",
  "image2":"https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641",
  "image3":"https://inews.gtimg.com/om_bt/Os3eJ8u3SgB3Kd-zrRRhgfR5hUvdwcVPKUTNO6O7sZfUwAA/641"}'
};

fetch('https://api.siliconflow.cn/v1/images/generations', options)
  .then(response => response.json())
  .then(response => console.log(response))
  .catch(err => console.error(err));
```

```
{
  "images": [
    {
      "url": "string"
    }
  ],
  "timings": {
    "inference": 0.1
  },
  "seed": 0
}
```

```
{
  "code": 20012,
  "message": "string",
  "data": "string"
}
```

```
{
  "message": "Request was rejected due to rate limiting. If you want more, please contact contact@siliconflow.cn. Details:TPM limit reached.",
  "data": "string"
}
```

```
{
  "code": 50505,
  "message": "Model service overloaded. Please try again later.",
  "data": "string"
}
```

