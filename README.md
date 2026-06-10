s&box render panel as texture and interact with uv position

**Technical Details**

The system was designed to support a large number of objects. Most of the optimization work is handled by **TargetPanelSystem.cs**, which manages frustum culling, distance calculations, focus detection, and frame rate monitoring to minimize panel refreshes. All these parameters can be configured directly from the component.

If you want to use a custom shader, make sure to use the same texture parameter name as the Complex shader: **`g_tColor`**.


**2D mouse support**

https://github.com/user-attachments/assets/113b71b2-8ce1-4f74-80c7-caa8fa32c168

<img width="1926" height="1088" alt="image" src="https://github.com/user-attachments/assets/85687ddc-c92d-4585-87cd-6b0c453fef6e" />


**3D support**

https://github.com/user-attachments/assets/fc636ddc-ca56-467e-8868-9d3f78deb0b0

https://github.com/user-attachments/assets/01f87218-089e-4054-ac93-df190e4713d4


