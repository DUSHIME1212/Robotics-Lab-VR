---
license: cc-by-4.0
viewer: false
task_categories:
- robotics
tags:
- simready
- robotics
- simulation
- usd
- openusd
- isaac-sim
- humanoid
- 3d
---

# Asimov 1 — SimReady USD

USD simulation asset for the Asimov 1 humanoid robot, prepared against the SimReady Robot-Body profile. This dataset contains the packaged USD asset and its validation, benchmark, and packaging metadata—not the full engineering source repository.

**Full project sources:** https://github.com/menloresearch/asimov-1 — mechanical and electrical designs, URDF-related sources, software, and project documentation. Those sources carry their own applicable licenses.

## Robot overview

| Specification | Value |
| --- | --- |
| Robot type | Humanoid |
| Project height | Approximately 1.2 m |
| Nominal project mass | 35 kg |
| Actuated degrees of freedom | 25 |

Height and nominal mass are project specifications, not newly measured simulation dimensions. The authored USD body masses sum to approximately **35.525 kg**. The package provides the robot asset, not a balance or locomotion controller.

## Package and loading

- Asset folder: [`asimov_1/`](asimov_1)
- Root USD: [`asimov_1/simready_usd/asimov_1.usd`](asimov_1/simready_usd/asimov_1.usd)
- Package ID: `simready.hf.Menlo.asimov-1-simready-usd.asimov_1.2026.10.20_00`
- Package license: **CC-BY-4.0**
- Tested with Isaac Sim **6.1.0.0** and SimReady benchmark **2026.8.0.post1**.

Package integrity: [package definition](asimov_1/com.nvidia.simready.packaging.json) · [bill of materials and SHA-256 hashes](asimov_1/.metadata/com.nvidia.simready.packaging.bom.json) · [detailed asset README](asset-description.txt).

### Download and open in Isaac Sim

With the [Hugging Face CLI](https://huggingface.co/docs/huggingface_hub/guides/cli) installed, download the complete package:

```bash
hf download Menlo/asimov-1-simready-usd \
  --repo-type dataset \
  --include 'asimov_1/*' \
  --local-dir ./asimov-1-simready
```

If access requires authentication, run `hf auth login` first. The download includes the configuration layers, hidden metadata and thumbnail directories; preserve their relative paths rather than copying only the root USD.

1. Launch Isaac Sim (tested with **6.1.0.0**).
2. Choose **File → Open** and select `asimov-1-simready/asimov_1/simready_usd/asimov_1.usd` beneath your download location.
3. To integrate the robot into an existing scene instead, add the root USD as a reference and configure the surrounding scene separately.

This opens the robot asset, not a complete simulation application. Supply ground, lighting and physics-scene settings as needed for your use case. Opening the USD or pressing Play does not provide a balance or locomotion controller. Validate the behaviour you need in your own engine and scene before relying on it.

Materials reference NVIDIA's runtime-provided `OmniPBR.mdl`, which is intentionally not redistributed here. Use an Isaac Sim/Omniverse installation that supplies that runtime library.
