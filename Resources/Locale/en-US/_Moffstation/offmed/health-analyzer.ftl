-health-analyzer-rating = { $rating ->
    [good] ([color=#00D3B8]good[/color])
    [okay] ([color=#30CC19]okay[/color])
    [poor] ([color=#bdcc00]poor[/color])
    [bad] ([color=#E8CB2D]bad[/color])
    [awful] ([color=#EF973C]awful[/color])
    [dangerous] ([color=#FF6C7F]dangerous[/color])
   *[other] (unknown)
    }

health-analyzer-window-entity-blood-pressure-text = Blood Pressure:
health-analyzer-window-entity-blood-oxygenation-text = Blood Saturation:

health-analyzer-window-entity-respiratory-rate-text-moffstation = Breathing:

health-analyzer-window-entity-brain-health-value-moffstation = {$value}% { -health-analyzer-rating(rating: $rating) }
health-analyzer-window-entity-heart-health-value-moffstation = {$value}% { -health-analyzer-rating(rating: $rating) }
health-analyzer-window-entity-lung-health-value-moffstation = {$value}% { -health-analyzer-rating(rating: $rating) }
health-analyzer-window-entity-heart-rate-value-moffstation = {$value}bpm { -health-analyzer-rating(rating: $rating) }
health-analyzer-window-entity-blood-oxygenation-value-moffstation = {$value}% { -health-analyzer-rating(rating: $rating) }
health-analyzer-window-entity-blood-pressure-value-moffstation = {$systolic}/{$diastolic} { -health-analyzer-rating(rating: $rating) }
health-analyzer-window-entity-blood-flow-value-moffstation = {$value}% { -health-analyzer-rating(rating: $rating) }
