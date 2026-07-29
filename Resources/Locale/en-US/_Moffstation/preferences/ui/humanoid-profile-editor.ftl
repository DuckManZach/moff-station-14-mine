# CD Height
humanoid-profile-editor-height-label = Height:
humanoid-profile-editor-reset-height-button = Reset
humanoid-profile-editor-department-jobs-label-moffstation = [font="DefaultBold" size=16][color=white]{$departmentName} jobs[/color][/font]

# Moffstation Tabs
moff-special-roles-tab = Special roles

moff-antag-label-moffstation = [font="DefaultBold" size=16][color={$color}]{$departmentName}[/color][/font]
moff-antag-search-placeholder = Search Special Roles...

moff-antag-footer-hint = Read faction info for more details
moff-antag-footer-info = Death to Nanotrasen!

# UI overhaul tabs
humanoid-profile-editor-identity-tab = Identity
humanoid-profile-editor-loadout-tab = Loadouts
humanoid-profile-editor-roles-tab = Roles

# Character editor
moff-profile-editing-title = Editing Profile
moff-profile-confirm-reset = Confirm reset?
moff-profile-body-section = Body
moff-profile-preview-clothing = Preview clothing

# Shared filter controls
moff-ui-clear = Clear
moff-ui-selected-only = Selected only

# Roles tab
moff-roles-unavailable-label = If none of your enabled jobs are available:
moff-roles-unavailable-stay = Stay in lobby
moff-roles-unavailable-overflow = Spawn as a {$overflowJob}
moff-roles-enabled-jobs = Enabled jobs
moff-roles-enabled-job-chip = {$job} ×
moff-roles-disable-job = Disable {$job}
moff-roles-no-jobs-enabled = No jobs enabled.
moff-roles-no-jobs-match = No jobs match these filters.
moff-roles-search-placeholder = Search jobs...
moff-roles-all-departments = All departments
moff-roles-loadout-button = Loadout

# Loadout tab
moff-loadout-editing-label = Editing:
moff-loadout-scope-universal = Universal
moff-loadout-categories = Categories
moff-loadout-all-items = All items
moff-loadout-search-placeholder = Search loadout items...
moff-loadout-reset-category = Reset category
moff-loadout-reset-loadout = Reset loadout
moff-loadout-reset-confirm = Confirm reset
moff-loadout-points = Points: [{$used}/{$max}]
moff-loadout-category-required = Required
moff-loadout-category-optional = Optional
moff-loadout-category-requires = Requires [{$required}] • Selected: [{$selected}]
moff-loadout-category-selected = Selected: [{$selected}]
moff-loadout-category-selected-max = Selected: [{$selected}/{$max}]

# Genders for pronoun manifest
gender-display = ({$gender ->
    [male] { humanoid-profile-editor-pronouns-male-text }
    [female] { humanoid-profile-editor-pronouns-female-text }
    [neuter] { humanoid-profile-editor-pronouns-neuter-text }
    *[other] { humanoid-profile-editor-pronouns-epicene-text }
})
