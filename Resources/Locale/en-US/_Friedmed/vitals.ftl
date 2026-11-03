offbrand-crew-monitoring-heart-rate = [color=white]{ $rate }[/color]bpm
offbrand-crew-monitoring-blood-pressure = [color=white]{ $systolic }[/color]/[color=white]{ $diastolic }[/color]
offbrand-crew-monitoring-spo2 = [color=white]{ $value }[/color]% { LOC($spo2) }

etco2-carbon-dioxide = EtCO2
etco2-ammonia = EtNH3
etco2-nitrous-oxide = EtN2O

spo2-oxygen = SpO2
spo2-nitrogen = SpN2

# vitals monitor

offbrand-vitals-pain = Pain
offbrand-vitals-pain-none = None
offbrand-vitals-spo2-value = {$value}%
offbrand-vitals-brain-activity = Brain Activity
offbrand-vitals-brain-activity-value = {$value}%

offbrand-vitals-respiratory-rate = Respiratory Rate
offbrand-vitals-respiratory-rate-value = {$value}/min
offbrand-vitals-etco2-value = {$value} mmHg

offbrand-vitals-blood-pressure = Blood Pressure
offbrand-vitals-blood-pressure-value = {$systolic}/{$diastolic}
offbrand-vitals-blood-volume = Blood Volume
offbrand-vitals-blood-volume-value = {$value}%

offbrand-vitals-heart-rate = Heartrate
offbrand-vitals-heart-rate-value = {$value} bpm

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
