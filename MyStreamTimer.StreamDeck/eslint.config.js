import eslint from "@eslint/js";
import tseslint from "typescript-eslint";

export default [
  {
    ignores: [
      "com.refractored.mystreamtimer.sdPlugin/bin/**",
      "com.refractored.mystreamtimer.sdPlugin/ui/sdpi-components.js",
      "coverage/**",
      "dist/**",
      "node_modules/**",
    ],
  },
  eslint.configs.recommended,
  {
    files: ["com.refractored.mystreamtimer.sdPlugin/ui/*.js"],
    languageOptions: {
      globals: {
        customElements: "readonly",
        document: "readonly",
        Event: "readonly",
        navigator: "readonly",
        SDPIComponents: "readonly",
      },
    },
  },
  ...tseslint.configs.recommendedTypeChecked.map((config) => ({
    ...config,
    files: ["**/*.ts"],
  })),
  {
    files: ["**/*.ts"],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
  },
];
