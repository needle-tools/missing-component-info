# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.6.0] - 2023-06-27
- Remove: display of serialized properties in the inspector, usage of GlobalObjectId

## [1.5.1] - 2023-06-24
- Change: Prevent serializing missing asset guid
- Change: Improve `Find Hidden Objects With Missing Scripts` menu item by selecting previously missing objects and logging object names

## [1.5.0] - 2023-06-07
- Add check for missing references in RenderSettings
- Add check for missing references in Lighting/LightmapSettings

## [1.4.1] - 2023-05-12
- Fixed components getting dirtied more often than they should
- Fixed rare exception on domain reload with EditorStyles being null

## [1.4.0] - 2023-05-12
- Added MissingReferences tooling, originally based on SuperScience's MissingReferenceFinder
- Added test API (`SceneScanner`) for missing references for use in TestRunner

## [1.3.0] - 2022-06-27
- Add menu item to ``Tools/Needle/`` to make invisible objects with missing scripts visible

## [1.2.2-exp] - 2022-06-13
- hide when editor is not expanded
- catch NotSupported exception when trying to find candidate types

## [1.2.0-exp] - 2022-06-13
- add foldout to find potential types

## [1.1.3] - 2022-06-10
- fix author, description, repo

## [1.1.2] - 2022-06-07
- add foldout to render serialized properties
- lazy load serialized properties (if foldout is expanded)

## [1.0.0] - 2022-06-07
- initial release