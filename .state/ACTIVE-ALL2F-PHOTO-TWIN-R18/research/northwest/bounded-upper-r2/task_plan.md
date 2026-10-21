# Bounded upper R2 correction

- [x] Read frozen new-finish payloads only; leave their bytes unchanged.
- [x] Exactly clip the three new finish meshes toY>=9.38, preserving source UV and all upper profile geometry.
- [x] Deliver one corrective payload plus three individual mesh geometries; explicitly withdraw the old lower band.

Root replaces only the first own append mesh and adds the two new bounded children. No base wall or collider change, source investigation, Unity/Assets/QA work. Stop.
