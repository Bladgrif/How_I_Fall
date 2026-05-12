default lust = 0
default romance = 0
default purity = 0
default corruption_level = 0
default self_control = 5
default suspicion = 0
default trust_masha = 0
default trust_artem = 0
default lera_interest = 0

default show_debug_stats = True


screen debug_stats_overlay():

    if show_debug_stats:

        frame:
            xalign 1.0
            yalign 0.0
            xoffset -20
            yoffset 20
            padding (16, 12, 16, 12)
            background Solid("#100d15cc")

            vbox:
                spacing 4

                text "DEBUG STATS" size 18 color "#ffd6ec"

                null height 4

                text "lust: [lust]" size 18 color "#f4edf3"
                text "romance: [romance]" size 18 color "#f4edf3"
                text "purity: [purity]" size 18 color "#f4edf3"
                text "corruption: [corruption_level]" size 18 color "#f4edf3"
                text "self_control: [self_control]" size 18 color "#f4edf3"
                text "suspicion: [suspicion]" size 18 color "#f4edf3"

                null height 4

                text "trust_masha: [trust_masha]" size 18 color "#f4edf3"
                text "trust_artem: [trust_artem]" size 18 color "#f4edf3"
                text "lera_interest: [lera_interest]" size 18 color "#f4edf3"


screen debug_stats_keys():
    key "K_F2" action ToggleVariable("show_debug_stats")


init python:
    config.overlay_screens.append("debug_stats_overlay")
    config.overlay_screens.append("debug_stats_keys")
