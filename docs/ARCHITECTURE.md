# Architecture — 0.1.1-alpha auditée en source

## Périmètre

Un plugin DLL chargé dans SimHub. Aucun daemon, serveur, exécutable auxiliaire ni accès réseau à l'exécution. Les exécutables éventuels de tests sont des outils de développement, pas une dépendance du plugin.

Le module ne dépend pas des classes internes d'Ambient Lights. Le build utilise les vraies DLL du SimHub installé sur la machine cible, sans les fournir ni les imiter. Compilation/chargement réels encore à valider.

## Flux et concurrence

```text
SimHub DataUpdate -> instantané de télémétrie (expiration 1 s)
                                              |
Capture interne -> RGB[60] + génération + date |
  GDI SDR          (expiration 750 ms)         |
                                              v
Sortie interne : fond -> alertes -> identification -> plafond constant + luminosité
                                                     -> 60 RGB -> Adalight
```

Les deux workers sont internes au processus SimHub. Aucune capture ni ouverture série sur son chemin critique DataUpdate. L'intervalle minimal ajouté après traitement est 67 ms pour la capture, 34 ms pour la sortie : ce ne sont pas des garanties de 15/30 images par seconde.

Chaque nouvelle géométrie/mode/armement invalide la capture par génération. Une ancienne acquisition terminée en retard ne peut être acceptée comme image de la nouvelle géométrie. Une capture périmée ou future est rejetée. Les acquisitions des moniteurs restent **séquentielles** dans un worker : un appel natif bloqué peut retarder les trois ; le worker de sortie ne doit pas conserver les vieilles couleurs indéfiniment.

Un verrou de propriété partagé empêche deux instances du plugin d'écrire pendant un rechargement SimHub. Changer port, aperçu, confirmation électrique ou budget désarme la sortie dans la même opération que le remplacement de sa configuration. La vérification finale d'état et le démarrage de l'écriture sont protégés ensemble ; un paquet déjà transmis ne peut pas être rappelé.

OFF et le mode édition annulent les tests précédents. L'édition conserve un port déjà ouvert, avec fond noir et identification possible, pour éviter des resets de carte à chaque rectangle. La première ouverture attend le démarrage du contrôleur, puis remet le parseur à un état connu. Les délais d'ouverture, test et reconnexion utilisent un chronomètre monotone.

## Capture GDI/SDR — pas DXGI

Énumération native des moniteurs ; contexte temporaire DPI par thread ; DC associé au moniteur ; DC mémoire compatible ; DIB top-down 32 bits ; StretchBlt ; GdiFlush avant lecture des pixels. Suppression des DC/bitmaps lors des erreurs, retraits d'écran et changements de géométrie. Les zones sont moyennées dans une image réduite, pas dans trois images 4K complètes copiées dans le code géré.

Cette révision remplace notamment l'ancien mélange d'un DC d'écran et d'un bitmap créé depuis un contexte générique : les contraintes multi-affichage de StretchBlt doivent être respectées. **Ces appels n'ont pas été exécutés sous Windows ici.** HDR, plein écran exclusif, rotations, multi-GPU, DPI mixtes et performances restent à tester. Les noms DISPLAYn ne sont pas des identifiants matériels persistants : vérifier le mapping après renumérotation Windows.

Une exception native grave peut affecter SimHub ; les threads ne créent pas une isolation de processus. Aucun Thread.Abort ni changement global de contexte DPI du processus.

## Mapping et télémétrie

Chaque LED 1–60 possède un écran et un rectangle normalisé. Défaut : DISPLAY1 central 21–40 ; DISPLAY2 gauche 1–20 ; DISPLAY3 droit 41–60.

Un rôle spatial (-1, 0, +1) est attribué à chaque écran. Pour un total pair N, trier les LED par position horizontale logique (rôle + centre X), puis Y et adresse pour départager les ex æquo ; prendre les N/2 premières pour la gauche et les N/2 dernières pour la droite. N=60 donne 30+30 sans recouvrement. Le test visuel doit confirmer que les zones sont placées au bon endroit physique. Les positions de capture servent donc aussi à définir les groupes d'alertes.

Drapeaux sur les LED sélectionnées ; alertes latérales prioritaires ; absence d'effet = retour au fond, pas une couche noire. Les données réelles dépendent des champs normalisés effectivement exposés par SimHub. Seuls booléens ou nombres binaires 0/1 sont acceptés. Enum, chaîne, getter en erreur ou valeur multi-état restent indisponibles, sans deviner leur sémantique. Les noms du spotter sont encore à valider dans le SDK/simulateur réel.

## Blanc, luminosité et plafond modélisé

Le préréglage chaud/froid et vert/magenta s'applique au fond **Blanc fixe** et à l'identification blanche, pas aux couleurs de l'écran, au mode Couleur fixe ou aux alertes. La correction est normalisée avant assemblage du fond.

Après composition, toutes les couleurs subissent un gain commun **constant pour un budget donné** :

```text
I_repos = 60 × 0,001 A
I_dynamique_max_modèle = 60 × 3 × 0,020 A = 3,6 A
gain_budget = clamp((budget - I_repos) / I_dynamique_max_modèle, 0, 1)
RGB_final = floor(RGB_composé × gain_budget × luminosité)
```

Contrairement à la 0.1.0, ce gain ne change pas avec chaque image : remplacer cinq blancs par du rouge ne fait pas monter la luminosité des autres LED. C'est volontairement conservateur ; une couleur peu consommatrice n'exploite pas forcément tout le budget. Le curseur reste progressif et les tests ne contournent pas le plafond.

Courant affiché : 60 × 1 mA + somme(R+G+B)/255 × 20 mA. **Modèle seulement**, sans mesure, sans courant du contrôleur, sans connaissance des vraies LED, câbles ou protections. Les flashes RGB de démarrage et les couleurs déjà mémorisées dans le firmware échappent à la limitation PC.

## Série et arrêt

Une trame de 186 octets contenant toujours 60 couleurs RGB à 115200 bauds. Aucun ACK d'affichage n'est disponible. Le statut distingue donc une écriture réussie d'une réception physiquement prouvée.

Après ouverture : attente de démarrage puis 186 octets nuls et une trame noire. Un banc C++ teste le parseur original avec une réception préalimentée, mais ne reproduit pas le temps UART ni les interruptions FastLED. À l'arrêt normal : dernier paquet noir et courte fenêtre bornée de vidage du tampon. Cela ne garantit ni livraison ni extinction après crash. Firmware inchangé, sans watchdog ni timeout de trame.

## Réglages et compilation

Configuration canonique séparée de l'aperçu temporaire du blanc : les sauvegardes différées, l'annulation et la fermeture ne doivent pas persister un essai. JSON enregistré atomiquement avec sauvegarde ; défaut OFF et confirmation électrique obligatoire. Changer les paramètres électriques retire la confirmation utilisateur.

C# 5 / .NET Framework 4.8. Le script efface les anciennes sorties locales avant contrôle, exécute les tests C#, compile dans une zone intermédiaire, vérifie l'identité de l'assembly et produit un manifeste. Aucune DLL SimHub copiée dans la livraison ; installation uniquement sur demande explicite, SimHub fermé. Ces scripts PowerShell n'ont pas été exécutés ici.
