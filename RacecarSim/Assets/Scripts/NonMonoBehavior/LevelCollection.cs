using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Information about a collection of related levels.
/// </summary>
public class LevelCollection
{
    #region Constants
    /// <summary>
    /// The build index of the main menu level.
    /// </summary>
    public const int MainMenuBuildIndex = 0;
    #endregion

    /// <summary>
    /// The name of the collection displayed to users.
    /// </summary>
    public string DisplayName;

    /// <summary>
    /// A shorter name for the collection which can be prepended to the names of the levels in the collection.
    /// </summary>
    public string ShortName;

    /// <summary>
    /// The levels in the collection.
    /// </summary>
    public LevelInfo[] Levels;

    /// <summary>
    /// The display names of the levels in the collection.
    /// </summary>
    public List<string> LevelNames
    {
        get
        {
            List<string> levelNames = new List<string>(this.Levels.Length);
            foreach (LevelInfo level in this.Levels)
            {
                levelNames.Add(level.DisplayName);
            }
            return levelNames;
        }
    }

    #region All Level Info
    public static readonly LevelCollection[] LevelCollections =
    {
        
        new LevelCollection()
        {
            DisplayName = "Sandbox Environments",
            ShortName = "Sandbox",
            Levels = new LevelInfo[]
            {
                new LevelInfo()
                {
                    DisplayName = "Demo World",
                    BuildIndex = 2,
                },
                new LevelInfo()
                {
                    DisplayName = "Color Camera Sandbox",
                    BuildIndex = 4,
                },
                new LevelInfo()
                {
                    DisplayName = "Depth Camera Sandbox",
                    BuildIndex = 7,
                },
                new LevelInfo()
                {
                    DisplayName = "LIDAR Sandbox",
                    BuildIndex = 13,
                    HelpMessage = "You can switch the 3rd person camera view by pressing the space bar"
                },
                new LevelInfo()
                {
                    DisplayName = "AR Marker Sandbox",
                    BuildIndex = 15,
                    HelpMessage = "Click a block to select it, then: left-click it to change it's tag, right-click it to change it's color, or scroll to rotate"
                },
            }
        },

        new LevelCollection()
        {
            DisplayName = "Neo Labs",
            ShortName = "NeoLabs",
            Levels = new LevelInfo[]
            {
                new LevelInfo()
                {
                    DisplayName = "Lab D: Driving in Mazes",
                    BuildIndex = 106,
                    NumCheckpoints = 4,
                    HelpMessage = "Press the TAB key to skip to the next checkpoint, collisions will reset you back to the checkpoint",
                    AutograderBuildIndex = 115,
                    AutograderLevelCode = "labd",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Zigzag",
                            Description = "Drive through the maze titled zigzag.",
                            MaxPoints = 5,
                            TimeLimit = 20
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Spiral",
                            Description = "Drive through the maze titled spiral.",
                            MaxPoints = 5,
                            TimeLimit = 30
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Hallway",
                            Description = "Drive through the maze titled hallway.",
                            MaxPoints = 5,
                            TimeLimit = 30
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Challenge: Maze",
                            Description = "Drive through the maze.",
                            MaxPoints = 5,
                            TimeLimit = 60
                        },
                    }
                },

                new LevelInfo()
                {
                    DisplayName = "Lab E: Stoplight Challenge",
                    BuildIndex = 107,
                    HelpMessage = "Click on the block to select it, then right-click it to change it's color",
                    AutograderBuildIndex = 119,
                    AutograderLevelCode = "labe",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Turn Right",
                            Description = "Turn right at the intersection and stop",
                            MaxPoints = 2,
                            TimeLimit = 15
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Turn Left",
                            Description = "Turn left at the intersection and stop",
                            MaxPoints = 2,
                            TimeLimit = 15
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Go Straight",
                            Description = "Drive through the intersection and stop",
                            MaxPoints = 2,
                            TimeLimit = 15
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Two Intersections",
                            Description = "Drive through the course",
                            MaxPoints = 2,
                            TimeLimit = 30
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Three Intersections",
                            Description = "Drive through the course",
                            MaxPoints = 2,
                            TimeLimit = 30
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Full Course",
                            Description = "Drive through the course",
                            MaxPoints = 5,
                            TimeLimit = 30
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Challenge: The City",
                            Description = "Make your way through the city",
                            MaxPoints = 5,
                            TimeLimit = 60
                        },
                    }
                },

                new LevelInfo()
                {
                    DisplayName = "Lab F: Line Follower",
                    BuildIndex = 108,
                    NumCheckpoints = 4,
                    IsRaceable = true,
                    MaxCars = 1,
                    HelpMessage = "Press the TAB key to skip to the next checkpoint, going down the wrong path resets you back to the checkpoint",
                    AutograderBuildIndex = 35,
                    AutograderLevelCode = "labf",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Gentle Turn",
                            Description = "Follow the red line for a 30 degree turn.",
                            MaxPoints = 1,
                            TimeLimit = 10
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Sharp Turn",
                            Description = "Follow the blue line for a 90 degree turn.",
                            MaxPoints = 1,
                            TimeLimit = 10
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Curved path",
                            Description = "Follow the green line for a winding path.",
                            MaxPoints = 3,
                            TimeLimit = 10
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Color Priority 1",
                            Description = "Follow the color priority red > green > blue.",
                            MaxPoints = 3,
                            TimeLimit = 10
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Color Priority 2",
                            Description = "Follow the color priority red > green > blue for a more complex path.",
                            MaxPoints = 10,
                            TimeLimit = 20
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Challenge: Perpendicular",
                            Description = "Identify and turn onto a line which is directly perpendicular to the car.",
                            MaxPoints = 2
                        },
                    }
                },

                new LevelInfo()
                {
                    DisplayName = "Lab G: Autonomous Parking",
                    BuildIndex = 109,
                    HelpMessage = "Left-Click on the screen to move the cone",
                    AutograderBuildIndex = 41,
                    AutograderLevelCode = "labg",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Far",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 4
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Close",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 4
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Very Far",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 3,
                            TimeLimit = 15
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Slight left",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 3,
                            TimeLimit = 15
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Far right",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 3,
                            TimeLimit = 15
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Near and left",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 3,
                            TimeLimit = 15
                        }
                    }
                },

                new LevelInfo()
                {
                    DisplayName = "Lab H: Cone Slalom",
                    BuildIndex = 110,
                    HelpMessage = "Drive on the left side of the blue cone and on the right side of the red cone",
                    AutograderBuildIndex = 64,
                    AutograderLevelCode = "labh",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Blue Cone",
                            Description = "Pass the left side of the blue cone.",
                            MaxPoints = 1
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Red Cone",
                            Description = "Pass the right side of the red cone.",
                            MaxPoints = 1
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Straight Course",
                            Description = "Complete the slalom course.",
                            MaxPoints = 3,
                            TimeLimit = 20
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Curved Course",
                            Description = "Complete the slalom course.",
                            MaxPoints = 3,
                            TimeLimit = 25
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Gates",
                            Description = "Drive through each cone gate.",
                            MaxPoints = 2,
                            TimeLimit = 15
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Full Course",
                            Description = "Complete the slalom course.",
                            MaxPoints = 10,
                            TimeLimit = 90,
                            TimeBonuses = new Vector2[]{ new Vector2(20, 1), new Vector2(30, 0.5f), new Vector2(45, 0), new Vector2(60, -1), new Vector2(float.PositiveInfinity, -2) }
                        }
                    }
                },

                new LevelInfo()
                {
                    DisplayName = "Lab I: Wall Follower",
                    BuildIndex = 111,
                    NumCheckpoints = 4,
                    IsRaceable = true,
                    MaxCars = 1,
                    HelpMessage = "Press the TAB key to skip to the next checkpoint",
                    AutograderBuildIndex = 78,
                    AutograderLevelCode = "labi",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "45 Degree",
                            Description = "Navigate the course without touching any walls.",
                            MaxPoints = 2,
                            TimeLimit = 12,
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "90 Degree",
                            Description = "Navigate the course without touching any walls.",
                            MaxPoints = 3,
                            TimeLimit = 20
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Tight",
                            Description = "Navigate the course without touching any walls.",
                            MaxPoints = 3,
                            TimeLimit = 30
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Posts",
                            Description = "Navigate the course without touching any posts.",
                            MaxPoints = 2,
                            TimeLimit = 30
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Full Course",
                            Description = "Navigate the course without touching any walls.",
                            MaxPoints = 5,
                            TimeLimit = 90,
                            TimeBonuses = new Vector2[]{ new Vector2(20, 1), new Vector2(30, 0.5f), new Vector2(45, 0), new Vector2(60, -1), new Vector2(float.PositiveInfinity, -2) }
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Challenge: Organic",
                            Description = "Navigate the course.",
                            MaxPoints = 5,
                            TimeLimit = 90,
                            TimeBonuses = new Vector2[]{ new Vector2(45, 0.5f), new Vector2(60, 0), new Vector2(float.PositiveInfinity, -0.5f) }
                        }
                    }
                },
            }
        },

        new LevelCollection()
        {
            DisplayName = "Legacy Labs",
            ShortName = "LegacyLabs",
            Levels = new LevelInfo[]
            {
                new LevelInfo()
                {
                    DisplayName = "Lab 1: Driving in Shapes",
                    BuildIndex = 3,
                    AutograderBuildIndex = 26,
                    AutograderLevelCode = "lab1",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Straight",
                            Description = "Drive straight when RT is down.",
                            MaxPoints = 0.5f
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Reverse",
                            Description = "Drive backward when LT is down.",
                            MaxPoints = 0.5f,
                            DefaultCameraIndex = 2
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Left",
                            Description = "Turn left when the left joystick is pushed to the left.",
                            MaxPoints = 0.5f
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Right",
                            Description = "Turn right when the left joystick is pushed to the right.",
                            MaxPoints = 0.5f
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Circle",
                            Description = "Drive in a clockwise circle when the A button is pressed.",
                            MaxPoints = 2,
                            TimeLimit = 15
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Square",
                            Description = "Drive in a clockwise square when the B button is pressed.",
                            MaxPoints = 2.5f,
                            TimeLimit = 25
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Figure 8",
                            Description = "Drive in a figure 8 when the X button is pressed.",
                            MaxPoints = 2.5f,
                            TimeLimit = 35
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Chosen Shape",
                            Description = "Drive in a shape of your choice when the Y button is pressed.",
                            MaxPoints = 1,
                            TimeLimit = 15,
                            DoNotProceedUntilStopped = true
                        }
                    }
                },
                new LevelInfo()
                {
                    DisplayName = "Lab 2a: Line Following",
                    BuildIndex = 5,
                    AutograderBuildIndex = 34,
                    AutograderLevelCode = "lab2a",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Straight",
                            Description = "Drive forward at full speed when RT is fully pressed.",
                            MaxPoints = 1,
                            IsRequired = true
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Gentle Turn",
                            Description = "Follow the red line for a 30 degree turn.",
                            MaxPoints = 1
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Sharp Turn",
                            Description = "Follow the blue line for a 90 degree turn.",
                            MaxPoints = 1
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Curved path",
                            Description = "Follow the green line for a winding path.",
                            MaxPoints = 1,
                            TimeLimit = 12
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Color Priority 1",
                            Description = "Follow the color priority red > green > blue.",
                            MaxPoints = 3
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Color Priority 2",
                            Description = "Follow the color priority red > green > blue for a more complex path.",
                            MaxPoints = 3,
                            TimeLimit = 20
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Extra: Perpendicular",
                            Description = "Identify and turn onto a line which is directly perpendicular to the car.",
                            MaxPoints = 0
                        },
                    }
                },
                new LevelInfo()
                {
                    DisplayName = "Lab 2b: Cone Parking",
                    BuildIndex = 6,
                    HelpMessage = "Left-click on the screen to move the cone",
                    AutograderBuildIndex = 41,
                    AutograderLevelCode = "lab2b",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Far",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 2
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Close",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 1
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Very Far",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 1,
                            TimeLimit = 15
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Slight left",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 2,
                            TimeLimit = 15
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Far right",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 2,
                            TimeLimit = 15
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Near and left",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 2,
                            TimeLimit = 15
                        }
                    }
                },
                new LevelInfo()
                {
                    DisplayName = "Lab 3a: Safety Stop",
                    BuildIndex = 8,
                    AutograderBuildIndex = 47,
                    AutograderLevelCode = "lab3a",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Full Speed",
                            Description = "Travel at full speed when there are no obstacles.",
                            MaxPoints = 1,
                            IsRequired = true
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Wall",
                            Description = "Avoid hitting the wall.",
                            MaxPoints = 2
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Blocks",
                            Description = "Avoid hitting the blocks.",
                            MaxPoints = 1
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Cone",
                            Description = "Avoid hitting the cone.",
                            MaxPoints = 1
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Off-centered Cone",
                            Description = "Avoid hitting the cone.",
                            MaxPoints = 2
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Gap",
                            Description = "Drive through the gap.",
                            MaxPoints = 1
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Ramp",
                            Description = "Drive up the ramp.",
                            MaxPoints = 2
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Extra: Cliff",
                            Description = "Avoid driving off the cliff.",
                            MaxPoints = 0
                        },
                    }
                },
                new LevelInfo()
                {
                    DisplayName = "Lab 3b: Cone Parking (Revisited)",
                    BuildIndex = 9,
                    HelpMessage = "Left-click on the screen to move the cone and scroll to resize the cone",
                    AutograderBuildIndex = 55,
                    AutograderLevelCode = "lab3b",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Normal Cone",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 1
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Large Cone",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 1
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Small Cone",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 1
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Slight left",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 1,
                            TimeLimit = 15
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Far right",
                            Description = "Park 30 cm away from the cone.",
                            MaxPoints = 1,
                            TimeLimit = 15
                        }
                    }
                },
                new LevelInfo()
                {
                    DisplayName = "Lab 3c: Wall Parking",
                    BuildIndex = 10,
                    AutograderBuildIndex = 60,
                    AutograderLevelCode = "lab3c",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Straight",
                            Description = "Align with and park 20 cm away from the wall.",
                            MaxPoints = 2.5f
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Slight Angle",
                            Description = "Align with and park 20 cm away from the wall.",
                            MaxPoints = 2.5f
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Moderate Angle",
                            Description = "Align with and park 20 cm away from the wall.",
                            MaxPoints = 2.5f
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Major Angle",
                            Description = "Align with and park 20 cm away from the wall.",
                            MaxPoints = 2.5f,
                            TimeLimit = 15
                        }
                    }
                },
                new LevelInfo()
                {
                    DisplayName = "Lab 4a: Safety Stop (Revisited)",
                    BuildIndex = 8,
                    AutograderBuildIndex = 73,
                    AutograderLevelCode = "lab4a",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Full Speed",
                            Description = "Travel at full speed when there are no obstacles.",
                            MaxPoints = 1,
                            IsRequired = true,
                            DefaultCameraIndex = 2
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Wall",
                            Description = "Avoid hitting the wall.",
                            MaxPoints = 1,
                            DefaultCameraIndex = 2
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Blocks",
                            Description = "Avoid hitting the blocks.",
                            MaxPoints = 1,
                            DefaultCameraIndex = 2
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Cone",
                            Description = "Avoid hitting the cone.",
                            MaxPoints = 1,
                            DefaultCameraIndex = 2
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Gap",
                            Description = "Drive through the gap.",
                            MaxPoints = 1,
                            DefaultCameraIndex = 2
                        }
                    }
                },
                new LevelInfo()
                {
                    DisplayName = "Lab 4b: Wall Following",
                    BuildIndex = 14,
                    IsRaceable = true,
                    NumCheckpoints = 2,
                    AutograderBuildIndex = 78,
                    AutograderLevelCode = "lab4b",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "45 Degree",
                            Description = "Navigate the course without touching any walls.",
                            MaxPoints = 2,
                            TimeLimit = 12,
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "90 Degree",
                            Description = "Navigate the course without touching any walls.",
                            MaxPoints = 3,
                            TimeLimit = 20
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Tight",
                            Description = "Navigate the course without touching any walls.",
                            MaxPoints = 3,
                            TimeLimit = 30
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Posts",
                            Description = "Navigate the course without touching any posts.",
                            MaxPoints = 2,
                            TimeLimit = 30
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Full Course",
                            Description = "Navigate the course without touching any walls.",
                            MaxPoints = 5,
                            TimeLimit = 90,
                            TimeBonuses = new Vector2[]{ new Vector2(20, 1), new Vector2(30, 0.5f), new Vector2(45, 0), new Vector2(60, -1), new Vector2(float.PositiveInfinity, -2) }
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Extra: Organic",
                            Description = "Navigate the course.",
                            MaxPoints = 0,
                            TimeLimit = 90,
                            TimeBonuses = new Vector2[]{ new Vector2(45, 0.5f), new Vector2(60, 0), new Vector2(float.PositiveInfinity, -0.5f) }
                        }
                    }
                },
                new LevelInfo()
                {
                    DisplayName = "Lab 5: AR Tag Decisions",
                    BuildIndex = 16,
                    IsRaceable = true,
                    NumCheckpoints = 2,
                    AutograderBuildIndex = 84,
                    AutograderLevelCode = "lab5",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "ID - Left",
                            Description = "Turn left upon seeing a marker with ID 0.",
                            MaxPoints = 1.5f
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "ID - Right",
                            Description = "Turn right upon seeing a marker with ID 1.",
                            MaxPoints = 1.5f
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Orientation - Left",
                            Description = "Turn left if a marker with ID 199 is oriented left.",
                            MaxPoints = 1.5f
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Orientation - Right",
                            Description = "Turn right if a marker with ID 199 is oriented right.",
                            MaxPoints = 1.5f
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Color - Red",
                            Description = "Upon seeing a marker with ID 2, follow the color of the marker.",
                            MaxPoints = 2
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Color - Blue",
                            Description = "Upon seeing a marker with ID 2, follow the color of the marker.",
                            MaxPoints = 2
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Full Course",
                            Description = "Complete the randomly generated course.",
                            MaxPoints = 5,
                            TimeLimit = 75,
                            TimeBonuses = new Vector2[]{ new Vector2(15, 1), new Vector2(25, 0.5f), new Vector2(40, 0), new Vector2(60, -1), new Vector2(float.PositiveInfinity, -2) }
                        }
                    }
                },
                new LevelInfo()
                {
                    DisplayName = "Lab 6a: Roll Prevention",
                    BuildIndex = 19,
                    HelpMessage = "The car's center of mass has been artificially raised dramatically"
                },
                new LevelInfo()
                {
                    DisplayName = "Lab 6b: Driving in Shapes (Revisited)",
                    BuildIndex = 3,
                },
                new LevelInfo()
                {
                    DisplayName = "Lab 6c: Speed Limit",
                    BuildIndex = 20,
                    IsRaceable = true
                },
                new LevelInfo()
                {
                    DisplayName = "Cone Slalom: Regular",
                    BuildIndex = 11,
                    IsRaceable = true,
                    AutograderBuildIndex = 64,
                    AutograderLevelCode = "p1",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Blue Cone",
                            Description = "Pass the left side of the blue cone.",
                            MaxPoints = 1
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Red Cone",
                            Description = "Pass the right side of the red cone.",
                            MaxPoints = 1
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Straight Course",
                            Description = "Complete the slalom course.",
                            MaxPoints = 3,
                            TimeLimit = 20
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Curved Course",
                            Description = "Complete the slalom course.",
                            MaxPoints = 3,
                            TimeLimit = 25
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Gates",
                            Description = "Drive through each cone gate.",
                            MaxPoints = 2,
                            TimeLimit = 15
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Full Course",
                            Description = "Complete the slalom course.",
                            MaxPoints = 5,
                            TimeLimit = 90,
                            TimeBonuses = new Vector2[]{ new Vector2(20, 1), new Vector2(30, 0.5f), new Vector2(45, 0), new Vector2(60, -1), new Vector2(float.PositiveInfinity, -2) }
                        }
                    }
                },
                new LevelInfo()
                {
                    DisplayName = "Cone Slalom: Hard",
                    BuildIndex = 12,
                    IsRaceable = true,
                    NumCheckpoints = 2,
                    AutograderBuildIndex = 70,
                    AutograderLevelCode = "p1hard",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Extra: Close",
                            Description = "Complete the slalom course.",
                            MaxPoints = 0,
                            TimeLimit = 30
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Extra: Hill",
                            Description = "Complete the slalom course.",
                            MaxPoints = 0,
                            TimeLimit = 30
                        },
                        new AutograderLevelInfo()
                        {
                            Title = "Full Course",
                            Description = "Complete the slalom course.",
                            MaxPoints = 0,
                            TimeLimit = 120,
                            TimeBonuses = new Vector2[]{ new Vector2(60, 1), new Vector2(75, 0.5f), new Vector2(90, 0), new Vector2(float.PositiveInfinity, -0.5f) }
                        }
                    }
                },

            }
        },

        new LevelCollection()
        {
            DisplayName = "Final Challenge",
            ShortName = "Final",
            Levels = new LevelInfo[]
            {
                new LevelInfo()
                {
                    DisplayName = "Mini Grand Prix: Spring 2025",
                    BuildIndex = 131,
                    IsRaceable = true,
                    NumCheckpoints = 4,
                    MaxCars = 2,
                },
                new LevelInfo()
                {
                    DisplayName = "Micro Grand Prix 2025",
                    BuildIndex = 130,
                    IsRaceable = true,
                    NumCheckpoints = 0,
                    MaxCars = 2,
                },
                new LevelInfo()
                {
                    DisplayName = "Mini Grand Prix: Fall 2024",
                    BuildIndex = 126,
                    IsRaceable = true,
                    NumCheckpoints = 1,
                    MaxCars = 2,
                },
                new LevelInfo()
                {
                    DisplayName = "Grand Prix 2024",
                    BuildIndex = 114,
                    IsRaceable = true,
                    NumCheckpoints = 5,
                    MaxCars = 4,
                },
                new LevelInfo()
                {
                    DisplayName = "Mini Grand Prix: Spring 2024",
                    BuildIndex = 113,
                    IsRaceable = true,
                    NumCheckpoints = 0,
                    MaxCars = 2,
                },
                new LevelInfo()
                {
                    DisplayName = "Mini Grand Prix: Fall 2023",
                    BuildIndex = 104,
                    IsRaceable = true,
                    NumCheckpoints = 0,
                    MaxCars = 4,
                },
                new LevelInfo()
                {
                    DisplayName = "Grand Prix 2022",
                    BuildIndex = 96,
                    IsRaceable = true,
                    NumCheckpoints = 7,
                    MaxCars = 4,
                },
                new LevelInfo()
                {
                    DisplayName = "Grand Prix 2021",
                    BuildIndex = 95,
                    IsRaceable = true,
                    NumCheckpoints = 8,
                    MaxCars = 4,
                },
                new LevelInfo()
                {
                    DisplayName = "Grand Prix 2020",
                    BuildIndex = 18,
                    IsRaceable = true,
                    NumCheckpoints = 5,
                    MaxCars = 4,
                    AutograderBuildIndex = 92,
                    AutograderLevelCode = "final",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Grad Prix 2020",
                            Description = "Navigate through the course.",
                            MaxPoints = 25,
                            TimeLimit = 360,
                            TimeBonuses = new Vector2[]{ new Vector2(105, 3), new Vector2(120, 2), new Vector2(150, 1), new Vector2(180, 0), new Vector2(240, -1), new Vector2(300, -3), new Vector2(float.PositiveInfinity, -5) }
                        }
                    }
                },
                new LevelInfo()
                {
                    DisplayName = "Time Trial 2020",
                    BuildIndex = 17,
                    IsRaceable = true,
                    NumCheckpoints = 3,
                    AutograderBuildIndex = 91,
                    AutograderLevelCode = "final",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Time Trial",
                            Description = "Navigate through the course.",
                            MaxPoints = 25,
                            TimeLimit = 300,
                            TimeBonuses = new Vector2[]{ new Vector2(75, 3), new Vector2(90, 2), new Vector2(120, 1), new Vector2(150, 0), new Vector2(180, -1), new Vector2(240, -3), new Vector2(float.PositiveInfinity, -5) }
                        }
                    }
                }, 
            }
        },

        new LevelCollection()
        {
            DisplayName = "Community Levels",
            ShortName = "PubLev",
            Levels = new LevelInfo[]
            {
                new LevelInfo()
                {
                    DisplayName = "[Greece] Time Trial 2025",
                    BuildIndex = 127,
                    IsRaceable = true,
                    MaxCars = 1,
                    AutograderBuildIndex = 128,
                    AutograderLevelCode = "final",
                    AutograderLevels = new AutograderLevelInfo[]
                    {
                        new AutograderLevelInfo()
                        {
                            Title = "Time Trial 2025",
                            Description = "Navigate through the course.",
                            MaxPoints = 25,
                            TimeLimit = 300,
                            TimeBonuses = new Vector2[]{ new Vector2(85, 3), new Vector2(100, 2), new Vector2(130, 1), new Vector2(160, 0), new Vector2(190, -1), new Vector2(250, -3), new Vector2(float.PositiveInfinity, -5) }                        }
                    }
                },
                new LevelInfo()
                {
                    DisplayName = "[Greece] Grand Prix 2025",
                    BuildIndex = 129,
                    IsRaceable = true,
                    NumCheckpoints = 4,
                    MaxCars = 4,
                }
            }
        },
    };
    #endregion

    /// <summary>
    /// Initializes static level-related fields.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by Unity")]
    private static void Initialize()
    {
        foreach (LevelCollection collection in LevelCollection.LevelCollections)
        {
            foreach (LevelInfo level in collection.Levels)
            {
                level.CollectionName = collection.ShortName;
                if (level.IsRaceable)
                {
                    level.WinableIndex = LevelInfo.WinableLevels.Count;
                    LevelInfo.WinableLevels.Add(level);
                }
            }
        }
    }
}
