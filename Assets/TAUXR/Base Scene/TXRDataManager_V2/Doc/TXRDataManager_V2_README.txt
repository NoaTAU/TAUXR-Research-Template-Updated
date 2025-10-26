=====================================================
          DATA MANAGER V2 — README
=====================================================
What is the TAUXR DataManager?
A data collection & export package that can be easily added to
any project to save relevant data to a local device.

It handles continuous logging of VR data
(head, gaze, hands, body, face expressions, etc.) 
and lets you add your own custom event tables.

You do NOT need to edit the core manager or schemas.  
Your interaction points are limited to:
- creating your own "custom data classes"
- adding custom "reporter" functions
- adding custom transforms to track in the inspector

For the full list of fields logged automatically by the template
(head/eyes/hands/body/face), see:
   data_sources_README.txt
=====================================================


1. HOW TO USE TXRDataManager_V2
-----------------------------------------------------

- **Custom data classes (events)**
  Create a simple C# class with public fields, 
  e.g. "ChoiceMade", "TrialStart", etc.
  This becomes a new CSV with those field names.
  Guidelines:
  1. Keep the access modifier `public`, first field should always be `TableName`.
  2. It is always recommended to add a log time field, e.g. `TimeSinceStart` for continuity with other files.
  3. Always add a constructor to the class that sets default values.

  Example:
      public class ChoiceEvent : CustomDataClass
      {
          public string TableName = "ChoiceEvents";
          public float TimeSinceStart;
          public int Trial;
          public string Option1Name;
          public string Option2Name;
          public string Choice;
          public float ReactionTime;

          public ChoiceEvent(int trial, string option1, string option2, string choice, float rt)
          {
              TimeSinceStart = Time.realtimeSinceStartup;
              Trial = trial;
              Option1Name = option1;
              Option2Name = option2;
              Choice = choice;
              ReactionTime = rt;
          }
      }

- **Reporter functions**
  Add helper functions in TXRDataManager_V2 to log your new class.  

  Example:
      public void LogChoice(int trial, string option1Name, string option2Name, string choice, float rt)
      {
          ChoiceEvent choiceEvent = new ChoiceEvent(trial, option1Name, option2Name, choice, rt);
          CustomCsvFromDataClass.Write(choiceEvent);
      }

- **Custom transforms**
  In the Unity inspector, assign transforms (objects) 
  you want to record positions/rotations for.
  Example: drag "Stimuli_A" into "Custom Transforms To Record" list.
  They will automatically appear as extra columns in
  ContinuousData.csv.

That’s it. ContinuousData.csv and FaceExpressions.csv 
are always recorded with the built-in schemas, you 
don’t need to edit them. For details of those columns, 
refer to data_sources_README.txt.


2. METADATA
-----------------------------------------------------

Each session is accompanied by one JSON file:

- session_metadata.json  
  Written automatically at runtime by SessionMetaWriter.cs.  
  Contains:
    * session_id
    * start time
    * device/platform info
    * enabled features (eyes, face, hands, controllers)
    * schema revision
    * compact map of data sources

Together, this guarantees reproducibility: you know 
exactly which build and session produced the data and under which 
settings.

(There is also a file called build_info.json generated at build time,
but it is not part of the output data files you will see.)


3. HOW IT WORKS (UNDER THE HOOD)
-----------------------------------------------------

The system is made of small scripts grouped by role.

A) Orchestrator
---------------
- TXRDataManager_V2.cs  
  The conductor. Sets up schemas, opens CSV files,
  and calls collectors every physics tick. 
  owns all the CsvRowWriters and calls them each frame.
  On quit, closes files and writes metadata.

B) Core Infrastructure
-----------------------
These are the building blocks that make CSV writing
work. You don’t normally edit them.
- SchemaBuilder.cs: defines column names for each CSV.
- ColumnIndex.cs: stores ordered column names and allows
  quick lookup by name or index.
- RowBuffer.cs: staging area for one row of data.
- CsvRowWriter.cs: writes one CSV (header + rows).
- CustomCsvFromDataClass.cs: lets you write a custom
  data class straight to CSV automatically.

C) Collectors
-------------
Collectors pull data from the VR system and fill rows.
- OVRNodesCollector.cs: device nodes (head, hands, etc.)
- OVREyesCollector.cs: eye gazes (angles, valid, confidence, shared time) + focused object & hit point via TXRPlayer
- OVRHandsCollector.cs: hand tracking, bones, confidence
- OVRBodyCollector.cs: body joints and calibration
- OVRFaceCollector.cs: face expression weights + validity
- CustomTransformsCollector.cs: positions/rotations of
  experiment-specific objects you register in inspector
- RecenterCollector.cs: detects recenter events

D) Metadata
-----------
- AutoBuildInfo.cs: runs in Unity Editor at build time,
  writes build_info.json, appends build id to version.
- BuildInfoLoader.cs: loads build_info.json at runtime.
- SessionMetaWriter.cs: writes session_metadata.json
  when session starts.

-----------------------------------------------------

Data collection flow:
- Collectors fill a RowBuffer with values for that tick.
- RowBuffer flushes to CsvRowWriter.
- CsvRowWriter writes it to disk (CSV file).
- Metadata scripts run in parallel, writing JSONs.

So the chain is:
Collectors → RowBuffer → CsvRowWriter → CSV files


4. FAQ
-----------------------------------------------------

Q: Do I need to edit DataManager_V2 or SchemaBuilder?  
A: No. They are prebuilt. You only add custom classes
   and reporter functions.

Q: Where do I find which columns are in the CSV?  
A: See data_sources_README.txt for ContinuousData and 
   FaceExpressions. Custom tables use your class fields.

Q: How do I add a new event table?  
A: Create a new data class that inherits from CustomDataClass
   with TableName and fields,
   then add a reporter function that instantiates it
   and calls CustomCsvFromDataClass.Write().

Q: Will missing values appear as zeros?  
A: No. Empty cells are left blank in CSV (meaning:
   “no data this frame”).

Q: How often is data logged?  
A: ContinuousData.csv and FaceExpressions.csv are logged 
   once per physics tick (Unity’s FixedUpdate). The tick
   rate is set in Project Settings → Time → Fixed Timestep 
   (default is 0.02 seconds = 50 Hz).  
   You can change this setting if you want a different
   logging frequency for continuous data.

   Custom data classes are different: they are logged
   **whenever your reporter function is called**. This means 
   you can log events at arbitrary times, regardless of the
   physics tick.

Q: What if Unity crashes—will I lose data?  
A: No, CsvRowWriter flushes each line to disk so files
   stay consistent.

Note: Enum/flag fields are written as strings (e.g., "High", "Calibrating", "Tracked|OrientationValid") for readability.

=====================================================
