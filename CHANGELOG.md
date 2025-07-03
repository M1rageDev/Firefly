# Changelog

## Unreleased

* Fix window duplication bug (thanks **@infradmin**)

## 1.0.5 - 2025-07-03

* Fix black trail bug
* Bring back missing bowshock
* Separate FireflyAPI into separate mod
* Introduce install checker for FireflyAPI


## 1.0.4 - 2025-07-02

* Create FireflyAPI, which allows modders to use the effect override system
* Introduce config versioning
* Add Space Shuttle System envelope (thanks **@giuliodondi**)
* Make the effect override system more universal, which should allow mods to create custom effects using Firefly
* Fix the particles (again), now they should be done for good
* Fix Gigantor XL solar panels having bugged effects
* Fix Communotron 16 having bugged effects


## 1.0.3 - 2025-04-10

* Add particle config
* Add particle editor
* Add Ervo config for MPE (thanks **@SPACEMAN9813**)
* Remove individual particle toggles from settings (replaced by particle editor)
* Improve smoke trail particles


## 1.0.2 - 2025-03-01

* Add configs for Kcalbeloh and OPM (thanks **@SPACEMAN9813**)
* Add Sol compatibility
* Add temporary workaround for drill envelope
* Make the smoke particles much faster and longer
* Make the dynamic pressure affect the effect strength more, fixing the RSS transition (again)
* Fix particle systems going in wrong direction in certain cases


## 1.0.1 - 2025-02-17

#### WARNING FOR PLANET PACK MAKERS:

This update makes some changes to the **planet pack** configs. Please make sure you update them.

* Add custom envelopes for benjee10's SOCK
* Add error/problem handling and list, warning users about incorrect installs or other issues
* Add warning about enabled stock effects
* Add additional message to the effect editor, informing to not use the simulation sliders in normal gameplay
* Add `transition_offset` to the planet pack configs
* Change planet pack config `speed_multiplier` to `strength_multiplier`
* Fix RSS reentry starting too late
* Fix graphics bug on Linux + Proton (Mac is still broken though), huge thanks to **@bmsbwd, @KlyithSA and @Cypherthe1st** for helping to find the fix


## 1.0.0.0 - 2025-02-05

* Initial Release