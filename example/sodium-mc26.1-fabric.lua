local recipe = {}

recipe.protocol = "v1"

recipe.meta = {
  name = "sodium-mc26.1-fabric",
  description = "The fastest and most compatible rendering optimization mod for Minecraft.",
  upstream_url = "https://modrinth.com/mod/sodium",
  category = "mod",
  license = "Polyform-Shield-1.0.0",
  version = "0.8.9",
  release = 1,
  epoch = 0,
  provides = {
    sodium = "0.8.9",
    indium = "1"
  },
  depends = {
    minecraft = ">=26.1 <26.2",
    fabricloader = ">=0.16.0"
  },
  conflicts = {
    embeddium = "*",
    optifabric = "*",
    canvas = "*",
    vulkanmod = "*",
    optimalaim = "<2.0.0",
    sodium_blendingregistry = "*",
    ocrenderfix_sodium = "*",
    betterfpsdist = "<=4.5",
    bobby = "<5.2.4",
    chunksfadein = "<2.0.2",
    cull_less_leaves = "<=1.3.0",
    cullleaves = "<=3.4.0",
    custom_hud = "<3.4.2",
    farsight = "<=4.3",
    iceberg = "<1.2.7",
    iris = "<=1.10.8",
    movingelevators = "<=1.4.7",
    notenoughcrashes = "<4.4.8",
    noxesium = "<2.3.3",
    reeses_sodium_options = "<2.0.2",
    fabric_api = "<0.145.1",
    sodium_extra = "<0.8.0",
    audio_engine_tweaks = "<1.2.12",
    sspb = "<6.0.0",
    moreculling = "<1.6.0-beta.2",
    simply_no_shading = "<7.6.2",
    betterend = "<=21.0.11",
    enchanteds_sodium_options = "<1.0.1"
  },
  replaces = {},
  recommends = {}
}

recipe.sources = {
  "https://cdn.modrinth.com/data/AANobbMI/versions/uGvVQBnw/sodium-fabric-0.8.9%2Bmc26.1.1.jar"
}

recipe.source_checksums = {
  "sha256:25ad95ba787bd7ff2a0deaf20a8f4642b91e23489d0e10933693d9963a6022f2"
}

function recipe.package()
  file.move("${SRCDIR}/sodium-fabric-0.8.9+mc26.1.1.jar", "${PKGDIR}/")
end

return recipe
