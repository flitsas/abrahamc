/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        flit: {
          bg: "#EAF2FF",
          modal: "#EEF5FF",
          card: "#FFFFFF",
          cyan: "#4FD4CC",
          blue: "#4F74C9",
          blueDark: "#162744",
          blueText: "#526FB8",
          green: "#70CF3A",
          warning: "#F05A35",
          danger: "#E43D30",
          draft: "#59677D",
          muted: "#7D8798",
          border: "#D9DEE8",
          tableHeader: "#F4F6FA",
        },
      },
      backgroundImage: {
        "flit-primary": "linear-gradient(90deg, #4FD4CC 0%, #4F74C9 100%)",
        "flit-sidebar": "linear-gradient(180deg, #4FD4CC 0%, #4F74C9 100%)",
        "flit-success": "linear-gradient(90deg, #4FD4CC 0%, #70CF3A 100%)",
        "flit-danger": "linear-gradient(90deg, #F05A35 0%, #E43D30 100%)",
      },
      borderRadius: {
        "flit-card": "18px",
        "flit-pill": "999px",
        "flit-sidebar": "56px",
      },
      boxShadow: {
        "flit-card": "0 8px 24px rgba(22, 39, 68, 0.08)",
        "flit-button": "0 10px 22px rgba(79, 116, 201, 0.22)",
      },
      fontFamily: {
        flit: [
          "Inter",
          "Poppins",
          "Montserrat",
          "system-ui",
          "-apple-system",
          "BlinkMacSystemFont",
          "Segoe UI",
          "sans-serif",
        ],
      },
    },
  },
  plugins: [],
};
