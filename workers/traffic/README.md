# Project-local SUMO reference foundation

Install: `python3 -m venv .tools/sumo/.venv` then `.tools/sumo/.venv/bin/pip install --only-binary=:all: eclipse-sumo==1.27.1 sumo-data==1.27.1`.

Reproduce from repository root: `.tools/sumo/.venv/bin/python workers/traffic/reproduce.py`.

The full `osm-busan-openworld.xml` is converted to a passenger-road network with original OSM IDs preserved. One emergency-class vehicle with maxSpeed 8 m/s, seed 4242, and a 1-second step follows an actual directed route from the road nearest the Central119 representative coordinate to the road nearest Busan Station surface bus stop OSM node 5004274563. Edges, not validated driveways, define departure and arrival. Trajectory coordinates are SUMO local UTM metres; see `receipt.json` location/projection/netOffset.

The network includes heuristic default lanes/speeds, connections and signal programs. Import warnings are retained in netconvert.log (including missing restrictions, discarded transit stops and sharp turns). This is a reproducible software connectivity fixture, not surveyed geometry, observed traffic, calibrated emergency travel time, or Unity integration. The source's original geographic extent includes outlying relation nodes; the converted network extent is separately recorded.

`reference-network/receipt.json` contains hashes, commands, source anchors, lane shapes, and completion evidence; `trajectory.xml`/`.csv` and `tripinfo.xml` retain actual simulator output. Do not interpret 169 simulation seconds as an operational response ETA. No GUI or global package installation was required.
