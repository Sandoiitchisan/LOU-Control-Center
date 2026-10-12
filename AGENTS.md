# Universal project instructions

- Work only in this folder. Do not modify the neighboring personal edition or its installation.
- Two distributions: portable (Data beside the executable) and installed (LocalAppData/LouControlCenterUniversal). Never load the personal edition’s LouControlCenter folders.
- The portable app creates no permanent task. The universal installer has its own AppId, Program Files folder and collector task; it includes signed PawnIO and optional Windows startup. The SYSTEM collector must not read commands or paths from user configuration.
- Read-only monitoring. Never call SetDefault / SetSoftware or write BIOS/GPU fan curves. Read docs/ARCHITECTURE.md.
- User assignments use provider, board, controller, sensor and inventory fingerprint. Never reassign by RPM, name alone or list position.
- Never run two LCD writers. The normal mutex is shared with the personal edition; --preview and --review-ui send no USB data.
- Keep rendering/transfer threads independent of WPF. Measure distinct frames transmitted during physical tests, not just target FPS.
- Temporary tests must not change the saved page interval. Packages must contain no Data, Review, logs or personal hardware identifiers.
- Verify the standalone executable, not only an SDK launch.
