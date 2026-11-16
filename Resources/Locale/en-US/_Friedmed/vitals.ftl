# vitals monitor

offbrand-vitals-pain = Pain
offbrand-vitals-pain-none = None
offbrand-vitals-brain-activity = Brain Activity
offbrand-vitals-brain-activity-value = {$value}%

offbrand-vitals-blood-volume = Blood Volume
offbrand-vitals-blood-volume-value = {$value}%

offbrand-vitals-stat-value = {$value}%

offbrand-vitals-capitalized = { CAPITALIZE($name) }
offbrand-vitals-whole-body = Whole body
offbrand-vitals-part-missing = Missing
offbrand-vitals-part-damaged = Damaged

offbrand-vitals-condition = { CAPITALIZE($name) }{ $count ->
    [1] {""}
   *[other] {" "}x{$count}
}
offbrand-vitals-condition-tended = {$condition} (tended)
offbrand-vitals-condition-damage = Damage: {$damage}

offbrand-vitals-reagent = { CAPITALIZE($reagent) } ({$amount}u)
offbrand-vitals-reagent-tooltip =
    In bloodstream: {$bloodstream}u
    Metabolites: {$metabolites}u
offbrand-vitals-non-medical-reagents = Non-medical reagents
