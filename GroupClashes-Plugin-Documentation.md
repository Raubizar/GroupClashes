# GroupClashes Plugin Documentation

## Overview
The GroupClashes plugin is a Navisworks add-in that automatically organizes and groups clash detection results into logical, hierarchically-structured groups that match real-world coordination workflows. This plugin transforms unorganized clash lists into meaningful categories for efficient clash resolution management.

## Plugin Information
- **Name**: Group Clashes
- **Developer**: BIM 42 (Simon Moreau)
- **Version**: 1.1.4
- **Compatible with**: Navisworks 2019-2025
- **Framework**: .NET Framework 4.8
- **Assembly**: GroupClashes.BM42.dll

## High-Level Workflow

### 1. User Interface Setup
The plugin appears as a **dockable panel** in Navisworks with the following components:
- **Ribbon button**: "Group Clashes" - launches the plugin interface
- **Clash test list**: Shows all available clash tests from the current document
- **Grouping dropdowns**: 
  - "Group by" - Primary grouping method
  - "Then by" - Optional sub-grouping method
- **Options**: "Keep existing groups" checkbox
- **Actions**: "Group" and "Ungroup" buttons

### 2. Grouping Process Flow
```
┌─ Select Clash Test(s) ─┐    ┌─ Choose Grouping Mode ─┐    ┌─ Execute Grouping ─┐
│ • View clash test list │ -> │ • Primary grouping      │ -> │ • Process clashes   │
│ • See Selection A/B    │    │ • Optional sub-grouping │    │ • Create groups     │
│ • Check clash count    │    │ • Keep existing option  │    │ • Apply to test     │
└────────────────────────┘    └─────────────────────────┘    └─────────────────────┘
```

### 3. Core Algorithm Steps
1. **Extract individual clash results** from selected clash test
2. **Create primary groups** based on chosen grouping mode
3. **Create sub-groups** (if "Then by" mode is selected)
4. **Remove single-clash groups** (groups with only 1 clash become individual clashes)
5. **Preserve existing groups** (if "Keep existing groups" option is checked)
6. **Replace original test** with new grouped version using Navisworks transactions

## Grouping Modes & Naming Conventions

### 📍 Level-Based Grouping (`GroupingMode.Level`)
Groups clashes by building level/floor using the active grid system.

**Group Name Format**: `{prefix}{LevelName}`

**Examples**:
- "Level 1"
- "Level 2" 
- "Basement"
- "Roof Level"
- "No Level" (for clashes not near any defined level)

**Implementation Details**:
- Uses `Application.MainDocument.Grids.ActiveSystem`
- Finds closest grid intersection to clash center point
- Orders groups by level elevation
- Requires active grid system in the document

### 🔍 Grid Intersection Grouping (`GroupingMode.GridIntersection`)
Groups clashes by structural grid intersections.

**Group Name Format**: `{prefix}{GridIntersectionName}`

**Examples**:
- "A1" (grid line A meets grid line 1)
- "B3"
- "C2"
- "No Grid intersection" (for clashes not near grid intersections)

**Implementation Details**:
- Uses `gridSystem.ClosestIntersection(clashCenter)`
- Orders by X position, then by level elevation
- Requires active grid system with defined intersections

### 🎯 Selection-Based Grouping (`GroupingMode.SelectionA` / `GroupingMode.SelectionB`)
Groups clashes by elements from either Selection A or Selection B of the clash test.

**Group Name Format**: `{prefix}{ElementDisplayName}`

**Examples**:
- "Wall_Foundation_123" (element name from model)
- "Pipe_Supply_456"
- "HVAC_Duct_789"
- "Unnamed Parent" (if element has no display name)
- "Empty clash" (if no element found)

**Implementation Details**:
- Uses `GetSignificantAncestorOrSelf()` to find meaningful parent element
- Prioritizes element display names over parent names
- Falls back to "Unnamed Parent" for elements without names
- Creates separate groups for empty/null clashes

### 📁 Model-Based Grouping (`GroupingMode.ModelA` / `GroupingMode.ModelB`)
Groups clashes by source model file.

**Group Name Format**: `{prefix}{ModelFileName}`

**Examples**:
- "Architectural.rvt"
- "MEP_Systems.dwg"
- "Structure.ifc"
- "Site.nwd"
- "Unnamed Model" (if model has no name)

**Implementation Details**:
- Uses `GetFileAncestor()` to find root model item
- Groups by the actual file that contains the clashing element
- Useful for discipline-based clash coordination

### 👤 Property-Based Grouping
Groups clashes by workflow properties assigned to clash results.

#### Assigned To (`GroupingMode.AssignedTo`)
**Group Name Format**: `{prefix}{AssignedToValue}`

**Examples**:
- "John.Smith@company.com"
- "MEP.Team@company.com"
- "Structural.Engineer@company.com"
- "Unspecified" (if not assigned to anyone)

#### Approved By (`GroupingMode.ApprovedBy`)
**Group Name Format**: `{prefix}{ApprovedByValue}`

**Examples**:
- "Project.Manager@company.com"
- "Lead.Coordinator@company.com"
- "Unspecified" (if not approved by anyone)

#### Status (`GroupingMode.Status`)
**Group Name Format**: `{prefix}{StatusValue}`

**Examples**:
- "New"
- "Active" 
- "Reviewed"
- "Approved"
- "Resolved"

**Implementation Details**:
- Uses clash result properties: `clashResult.AssignedTo`, `clashResult.ApprovedBy`, `clashResult.Status`
- Converts status enum to string representation
- Defaults to "Unspecified" for empty/null values

## Hierarchical Sub-Grouping

When using the **"Then by"** option, the plugin creates nested group structures:

**Example: Level (Primary) + Selection A (Secondary)**:
```
📁 Level 1                    (Primary: Level)
  ├── 📁 Level 1_Wall_123     (Sub: Selection A)
  ├── 📁 Level 1_Pipe_456     (Sub: Selection A)
  └── 📁 Level 1_Beam_789     (Sub: Selection A)

📁 Level 2                    (Primary: Level)
  ├── 📁 Level 2_HVAC.rvt     (Sub: Model A)
  └── 📁 Level 2_Plumbing.rvt (Sub: Model A)
```

**Naming Convention for Sub-groups**: `{PrimaryGroupName}_{SubGroupName}`

## Smart Features

### 📊 Group Optimization
- **Single-clash group removal**: Groups containing only one clash are dissolved, and the clash becomes an individual item
- **Existing group preservation**: When "Keep existing groups" is checked, previously created groups are maintained
- **Empty clash handling**: Clashes without valid grouping criteria are handled gracefully with fallback names

### 🔄 Dynamic User Interface
- **Grid system detection**: Grid-based grouping options (Level, Grid Intersection) only appear when an active grid system exists in the document
- **Real-time monitoring**: Interface updates automatically when clash tests are modified, added, or removed
- **Multi-selection support**: Can process multiple clash tests simultaneously
- **Progress tracking**: Shows progress bar during long operations with cancellation support

### 📈 Performance Features
- **Transaction-based operations**: Uses Navisworks transactions for safe, undoable operations
- **Efficient copying**: Creates copies of clash results rather than moving originals to prevent data loss
- **Memory management**: Proper disposal of temporary objects and progress indicators

## Technical Implementation Details

### Core Functions

#### `GroupClashes(ClashTest selectedClashTest, GroupingMode groupingMode, GroupingMode subgroupingMode, bool keepExistingGroups)`
Main entry point that orchestrates the entire grouping process.

#### `CreateGroup(ref List<ClashResultGroup> clashResultGroups, GroupingMode groupingMode, List<ClashResult> clashResults, string initialName)`
Factory method that delegates to specific grouping functions based on the selected mode.

#### `ProcessClashGroup(List<ClashResultGroup> clashGroups, List<ClashResult> ungroupedClashResults, ClashTest selectedClashTest)`
Final step that applies the grouped structure back to the Navisworks clash test using transactions.

### Helper Functions
- `GetIndividualClashResults()`: Extracts all individual clash results from a test, handling existing groups
- `RemoveOneClashGroup()`: Identifies and removes groups with only one clash
- `BackupExistingClashGroups()`: Preserves existing groups when the option is enabled
- `GetSignificantAncestorOrSelf()`: Finds meaningful parent elements for selection-based grouping
- `GetFileAncestor()`: Traces elements back to their source model files

## Use Cases and Benefits

### 🏗️ Discipline-Based Coordination
**Use Case**: Group clashes by model source (Architecture vs MEP vs Structure)
**Benefit**: Teams can focus on clashes involving their specific discipline
**Grouping Mode**: Model A or Model B

### 📍 Zone-Based Resolution
**Use Case**: Group clashes by building level or grid intersection
**Benefit**: Enables floor-by-floor or zone-by-zone clash resolution sessions
**Grouping Mode**: Level or Grid Intersection

### 👷 Workflow Management
**Use Case**: Group clashes by assignment or approval status
**Benefit**: Track progress and responsibility distribution across project teams
**Grouping Mode**: Assigned To, Approved By, or Status

### 🔧 Element-Type Coordination
**Use Case**: Group clashes by specific building elements (walls, pipes, ducts, etc.)
**Benefit**: Focus resolution efforts on specific building systems
**Grouping Mode**: Selection A or Selection B

### 🎯 Hierarchical Organization
**Use Case**: Combine multiple grouping criteria (e.g., Level + Model, Grid + Selection)
**Benefit**: Create detailed organizational structures for complex projects
**Grouping Mode**: Primary + "Then by" secondary grouping

## Installation and Deployment

### File Structure
```
GroupClashes.BM42.bundle/
├── PackageContents.xml          (Plugin manifest)
└── Contents/
    ├── 2019/
    │   └── GroupClashes.BM42.dll
    ├── 2020/
    │   └── GroupClashes.BM42.dll
    ├── 2021/
    │   └── GroupClashes.BM42.dll
    ├── 2022/
    │   └── GroupClashes.BM42.dll
    ├── 2023/
    │   └── GroupClashes.BM42.dll
    ├── 2024/
    │   ├── GroupClashes.BM42.dll
    │   ├── Images/
    │   │   ├── GroupClashesIcon_Large.ico
    │   │   └── GroupClashesIcon_Small.ico
    │   └── en-US/
    │       ├── GroupClashes.name
    │       └── GroupClashes.xaml
    └── 2025/
        ├── GroupClashes.BM42.dll
        ├── Images/
        │   ├── GroupClashesIcon_Large.ico
        │   └── GroupClashesIcon_Small.ico
        └── en-US/
            ├── GroupClashes.name
            └── GroupClashes.xaml
```

### Deployment Location
```
%APPDATA%\Autodesk\ApplicationPlugins\GroupClashes.BM42.bundle\
```

### Multi-Version Support
The plugin supports multiple Navisworks versions through conditional compilation:
- **2019**: .NET Framework 4.7
- **2020**: .NET Framework 4.7
- **2021**: .NET Framework 4.7.2
- **2022**: .NET Framework 4.7.2
- **2023**: .NET Framework 4.7.2
- **2024**: .NET Framework 4.7.2
- **2025**: .NET Framework 4.8

## Development Information

### Build Configurations
- Multiple year-specific debug/release configurations (2019Debug through 2025Debug)
- Automatic deployment to ApplicationPlugins directory
- Conditional assembly references based on target Navisworks version
- PostBuild script for code signing and release packaging

### Key Dependencies
- **Autodesk.Navisworks.Api.dll**: Core Navisworks API
- **Autodesk.Navisworks.Clash.dll**: Clash detection functionality
- **System.Windows.Forms**: UI components
- **PresentationFramework**: WPF interface elements

### Plugin Architecture
- **CommandHandlerPlugin**: Main plugin entry point with ribbon integration
- **DockPanePlugin**: Dockable panel interface
- **UserControl**: WPF-based user interface
- **Transaction-based operations**: Safe, undoable modifications to Navisworks data

## Troubleshooting

### Common Issues
1. **Grid options not available**: Ensure the document has an active grid system defined
2. **Plugin not loading**: Verify correct .NET Framework version and Navisworks API references
3. **Grouping fails**: Check that clash test contains clash results and is not empty
4. **Performance issues**: For large clash tests (>1000 clashes), consider grouping in smaller batches

### Debug Information
- Plugin uses debug output for troubleshooting (`System.Diagnostics.Debug.WriteLine`)
- Progress indicators provide status during long operations
- Transaction system ensures operations can be undone if issues occur

---

*Documentation generated for GroupClashes Plugin v1.1.4*  
*Compatible with Navisworks 2019-2025*  
*Last updated: August 7, 2025*
