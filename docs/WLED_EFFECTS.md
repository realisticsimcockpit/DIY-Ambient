# Animations visuellement compatibles avec WLED

Références étudiées le 20 septembre 2026 :

- catalogue officiel des effets : https://kno.wled.ge/features/effects/
- dépôt officiel : https://github.com/wled/WLED
- moteur d'effets officiel : https://github.com/wled/WLED/blob/main/wled00/FX.cpp
- licence WLED EUPL-1.2 : https://github.com/wled/WLED/blob/main/LICENSE

WLED annonce plus de 200 effets, dont des effets audio et matriciels qui ne correspondent pas à cette installation linéaire de 60 LED. La collection retenue pour le cockpit contient quatre effets 1D : Colorloop, Rainbow, Fire Flicker et Loading.

Pour Loading, l'option `Cycle aléatoire des couleurs` reproduit le rôle de la palette WLED `pal:1` (Random Cycle) : le fond passe progressivement entre plusieurs palettes tandis que le motif Loading conserve sa couleur principale. À la première sélection de Loading, la vitesse 136, le fondu 91 et l'orange `[255,160,0]` sont appliqués comme valeurs de départ.

L'implémentation de `AnimatedEffects.cs` a été réécrite pour le moteur .NET du plugin. Elle ne contient pas le code source WLED sous EUPL. Elle vise le même comportement visuel et les mêmes paramètres usuels, mais ne prétend pas être une reproduction binaire image par image du firmware WLED, dont le temps d'exécution, les palettes, l'état pseudo-aléatoire et la fréquence de rendu diffèrent.

Toutes les images animées passent ensuite par le plafond commun de luminosité et de puissance. Les alertes SimHub remplacent uniquement les positions de télémétrie sélectionnées, puis le fond animé revient automatiquement.
